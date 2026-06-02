// Copyright (c) Microsoft. All rights reserved.

use std::collections::btree_map::Entry;

use crate::RuntimeSettings;

pub(crate) fn agent_spec(
    settings: &mut crate::docker::Settings,
) -> Result<(), Box<dyn std::error::Error>> {
    // Set vol mounts for workload and management sockets.
    agent_vol_mount(settings)?;

    // Set environment variables that are specific to Moby/Docker.
    agent_env(settings);

    // Set networking config specific to Moby/Docker.
    agent_networking(settings);

    // Set labels for Edge Agent.
    agent_labels(settings);

    Ok(())
}

fn agent_vol_mount(
    settings: &mut crate::docker::Settings,
) -> Result<(), Box<dyn std::error::Error>> {
    let mut create_options = settings.agent().config().create_options().clone();
    let host_config = create_options.host_config.get_or_insert_default();
    let binds = host_config.binds.get_or_insert_default();

    let mgmt_uri = settings.connect().management_uri();

    let workload_connect_uri = settings.connect().workload_uri();

    let workload_listen_uri = {
        let mut path = settings.homedir().canonicalize()?;

        path.push("mnt");
        path.push(format!("{}.sock", settings.agent().name()));

        let path = path.to_str().ok_or("invalid workload socket path")?;
        url::Url::parse(&format!("unix://{path}")).expect("workload uri should be valid")
    };

    // If the url is a domain socket URL, then vol mount it into the container.
    for (connect_uri, listen_uri) in &[
        (mgmt_uri, mgmt_uri),
        (workload_connect_uri, &workload_listen_uri),
    ] {
        if connect_uri.scheme() == "unix" {
            let connect_path = std::path::Path::new(connect_uri.path()).to_path_buf();
            let connect_path = connect_path.to_string_lossy();

            let listen_path = std::path::Path::new(listen_uri.path()).to_path_buf();
            let listen_path = listen_path.to_string_lossy();

            let bind = format!("{listen_path}:{connect_path}");
            if !binds.contains(&bind) {
                binds.push(bind);
            }
        }
    }

    if !binds.is_empty() {
        settings
            .agent_mut()
            .config_mut()
            .set_create_options(create_options);
    }

    Ok(())
}

fn agent_env(settings: &mut crate::docker::Settings) {
    let network_id = settings.moby_runtime().network().name().to_string();
    settings
        .agent_mut()
        .env_mut()
        .insert("NetworkId".to_string(), network_id);
}

fn agent_networking(settings: &mut crate::docker::Settings) {
    let network_id = settings.moby_runtime().network().name().to_owned();

    let mut create_options = settings.agent().config().create_options().clone();

    let network_config = create_options.networking_config.get_or_insert_default();

    let endpoints_config = network_config.endpoints_config.get_or_insert_default();

    if let Entry::Vacant(entry) = endpoints_config.entry(network_id) {
        entry.insert(Default::default());

        settings
            .agent_mut()
            .config_mut()
            .set_create_options(create_options);
    }
}

fn agent_labels(settings: &mut crate::docker::Settings) {
    let mut create_options = settings.agent().config().create_options().clone();

    let labels = create_options.labels.get_or_insert_default();

    // IoT Edge reserves the label prefix "net.azure-devices.edge" for its own purposes
    // so we'll simply overwrite any matching labels created by the user.
    labels.insert(
        "net.azure-devices.edge.create-options".to_string(),
        "{}".to_string(),
    );
    labels.insert("net.azure-devices.edge.env".to_string(), "{}".to_string());
    labels.insert(
        "net.azure-devices.edge.original-image".to_string(),
        settings.agent().config().image().to_string(),
    );
    labels.insert(
        "net.azure-devices.edge.owner".to_string(),
        "Microsoft.Azure.Devices.Edge.Agent".to_string(),
    );

    settings
        .agent_mut()
        .config_mut()
        .set_create_options(create_options);
}
