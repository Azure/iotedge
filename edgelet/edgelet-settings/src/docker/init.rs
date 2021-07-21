// Copyright (c) Microsoft. All rights reserved.

use crate::RuntimeSettings;

pub(crate) fn agent_spec(
    settings: &mut crate::docker::Settings,
) -> Result<(), Box<dyn std::error::Error>> {
    // Set vol mounts for workload and management sockets.
    // agent_vol_mount(settings)?;

    // // Set environment variables that are specific to Moby/Docker.
    // agent_env(settings);

    // // Set networking config specific to Moby/Docker.
    // agent_networking(settings)?;

    // // Set labels for Edge Agent.
    // agent_labels(settings)?;

    Ok(())
}

// fn agent_vol_mount(
//     settings: &mut crate::docker::Settings,
// ) -> Result<(), Box<dyn std::error::Error>> {
//     let create_options = settings.agent().config().clone_create_options()?;
//     let host_config = create_options
//         .host_config()
//         .cloned()
//         .unwrap_or_else(docker::models::HostConfig::new);
//     let mut binds = host_config.binds().map_or_else(Vec::new, ToOwned::to_owned);

//     // If the url is a domain socket URL, then vol mount it into the container.
//     for uri in &[
//         settings.connect().management_uri(),
//         settings.connect().workload_uri(),
//     ] {
//         if uri.scheme() == "unix" {
//             let path = uri.to_uds_file_path()?;
//             let path = path
//                 .to_str()
//                 .ok_or_else(|| ErrorKind::InvalidSocketUri(uri.to_string()))?
//                 .to_string();
//             let bind = format!("{}:{}", &path, &path);
//             if !binds.contains(&bind) {
//                 binds.push(bind);
//             }
//         }
//     }

//     if !binds.is_empty() {
//         let host_config = host_config.with_binds(binds);
//         let create_options = create_options.with_host_config(host_config);

//         settings
//             .agent_mut()
//             .config_mut()
//             .set_create_options(create_options);
//     }

//     Ok(())
// }
