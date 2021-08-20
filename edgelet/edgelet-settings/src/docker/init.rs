// Copyright (c) Microsoft. All rights reserved.

use crate::RuntimeSettings;

pub(crate) fn agent_spec(settings: &mut crate::docker::Settings) {
    // Set vol mounts for workload and management sockets.
    agent_vol_mount(settings);

    // Set environment variables that are specific to Moby/Docker.
    agent_env(settings);

    // Set networking config specific to Moby/Docker.
    agent_networking(settings);

    // Set labels for Edge Agent.
    agent_labels(settings);
}

fn agent_vol_mount(settings: &mut crate::docker::Settings) {
    let create_options = settings.agent().config().create_options().clone();
    let host_config = create_options
        .host_config()
        .cloned()
        .unwrap_or_else(docker::models::HostConfig::new);
    let mut binds = host_config.binds().map_or_else(Vec::new, ToOwned::to_owned);

    // If the url is a domain socket URL, then vol mount it into the container.
    for uri in &[
        settings.connect().management_uri(),
        settings.connect().workload_uri(),
    ] {
        if uri.scheme() == "unix" {
            let path = std::path::Path::new(uri.path()).to_path_buf();
            let path = path.to_string_lossy();

            let bind = format!("{}:{}", path, path);
            if !binds.contains(&bind) {
                binds.push(bind);
            }
        }
    }

    if !binds.is_empty() {
        let host_config = host_config.with_binds(binds);
        let create_options = create_options.with_host_config(host_config);

        settings
            .agent_mut()
            .config_mut()
            .set_create_options(create_options);
    }
}

fn agent_env(settings: &mut crate::docker::Settings) {
    let network_id = settings.moby_runtime().network().name().to_string();
    settings
        .agent_mut()
        .env_mut()
        .insert("NetworkId".to_string(), network_id);
}

fn agent_networking(settings: &mut crate::docker::Settings) {
    let network_id = settings.moby_runtime().network().name().to_string();

    let create_options = settings.agent().config().create_options().clone();

    let mut network_config = create_options
        .networking_config()
        .cloned()
        .unwrap_or_else(docker::models::ContainerCreateBodyNetworkingConfig::new);

    let mut endpoints_config = network_config
        .endpoints_config()
        .cloned()
        .unwrap_or_else(std::collections::BTreeMap::new);

    if !endpoints_config.contains_key(network_id.as_str()) {
        endpoints_config.insert(network_id, docker::models::EndpointSettings::new());
        network_config = network_config.with_endpoints_config(endpoints_config);
        let create_options = create_options.with_networking_config(network_config);

        settings
            .agent_mut()
            .config_mut()
            .set_create_options(create_options);
    }
}

fn agent_labels(settings: &mut crate::docker::Settings) {
    let create_options = settings.agent().config().create_options().clone();

    let mut labels = create_options
        .labels()
        .cloned()
        .unwrap_or_else(std::collections::BTreeMap::new);

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

    let create_options = create_options.with_labels(labels);

    settings
        .agent_mut()
        .config_mut()
        .set_create_options(create_options);
}
