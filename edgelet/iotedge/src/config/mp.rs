// Copyright (c) Microsoft. All rights reserved.

//! This subcommand takes the super-config file, converts it into the individual services' config files,
//! writes those files, and restarts the services.

use std::path::Path;

use aziotctl_common::config as common_config;

use super::super_config;

pub fn execute(
    connection_string: String,
    out_config_file: &Path,
    force: bool,
) -> Result<(), std::borrow::Cow<'static, str>> {
    if !force && out_config_file.exists() {
        return Err(format!(
            "\
File {} already exists. Azure IoT Edge has already been configured.

To have the configuration take effect, run:

    iotedge config apply

To reconfigure IoT Edge, run:

    iotedge config mp --force
",
            out_config_file.display()
        )
        .into());
    }

    let config = super_config::Config {
        parent_hostname: None,

        trust_bundle_cert: None,

        aziot: common_config::super_config::Config {
            hostname: None,

            provisioning: common_config::super_config::Provisioning {
                always_reprovision_on_startup: false,
                provisioning: common_config::super_config::ProvisioningType::Manual {
                    inner: common_config::super_config::ManualProvisioning::ConnectionString {
                        connection_string,
                    },
                },
            },

            localid: None,

            aziot_keys: Default::default(),

            preloaded_keys: Default::default(),

            cert_issuance: Default::default(),

            preloaded_certs: Default::default(),

            endpoints: Default::default(),
        },

        agent: super_config::default_agent(),

        connect: Default::default(),
        listen: Default::default(),

        watchdog: Default::default(),

        edge_ca: None,

        moby_runtime: Default::default(),
    };
    let config = toml::to_vec(&config)
        .map_err(|err| format!("could not serialize system config: {}", err))?;

    let user = nix::unistd::User::from_uid(nix::unistd::Uid::current())
        .map_err(|err| format!("could not query current user information: {}", err))?
        .ok_or_else(|| "could not query current user information")?;

    common_config::write_file(&out_config_file, &config, &user, 0o0600)
        .map_err(|err| format!("{:?}", err))?;

    println!("Azure IoT Edge has been configured successfully!");
    println!(
        "The configuration has been written to {}",
        out_config_file.display()
    );
    println!("To apply the new configuration to services, run:");
    println!();
    println!(
        "    iotedge config apply -c '{}'",
        out_config_file.display()
    );
    println!();
    println!("WARNING: This configuration is not suitable when using IoT Edge as a gateway.");

    Ok(())
}
