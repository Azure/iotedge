// Copyright (c) Microsoft. All rights reserved.

//! This subcommand imports an iotedged config (v1.1 and below) into the super-config for 1.2+.
//!
//! Notes:
//!
//! - This implementation assumes that Microsoft's implementation of libaziot-keys is being used, in that it generates the keyd config
//!   with the `aziot_keys.homedir_path` property set, and with validation that the preloaded keys must be file paths or `file://` URIs.
//!
//! - PKCS#11 is not set up since iotedged did not support it. The user needs to configure it manually, which includes
//!   configuring their PKCS#11 hardware and library as well as transferring any filesystem keys to the hardware as they want.

mod old_config;

use std::path::{Path, PathBuf};

use config::Config;

use edgelet_utils::YamlFileSource;

use aziotctl_common::config as common_config;

use super::super_config;

const AZIOT_EDGED_LISTEN_MGMT_SOCKET_ACTIVATED_URI: &str = "fd://aziot-edged.mgmt.socket";
const AZIOT_EDGED_LISTEN_WORKLOAD_SOCKET_ACTIVATED_URI: &str = "fd://aziot-edged.workload.socket";

pub fn execute(
    old_config_file: &Path,
    new_config_file: &Path,
    force: bool,
) -> Result<(), std::borrow::Cow<'static, str>> {
    // In production, the command needs to run as root. But it's convenient for developers to run as the current user.
    //
    // So if this is a debug build, use the current user. Otherwise, tell the user to re-run as root.
    let root_user = {
        let current_uid = nix::unistd::Uid::current();
        if current_uid.is_root() {
            let root_user = nix::unistd::User::from_uid(current_uid)
                .map_err(|err| format!("could not query root user information: {}", err))?
                .ok_or("could not query root user information")?;

            root_user
        } else if cfg!(debug_assertions) {
            let current_user = nix::unistd::User::from_uid(nix::unistd::Uid::current())
                .map_err(|err| format!("could not query current user information: {}", err))?
                .ok_or("could not query current user information")?;
            current_user
        } else {
            return Err("this command must be run as root".into());
        }
    };

    if !force && new_config_file.exists() {
        return Err(format!(
            "\
File {} already exists. Azure IoT Edge has already been configured.

To have the configuration take effect, run:

    sudo iotedge config apply

To reconfigure IoT Edge, run:

    sudo iotedge config import --force
",
            new_config_file.display()
        )
        .into());
    }

    let old_master_encryption_key_path = {
        // libiothsm derived the filenames for encryption keys from their alias (see `normalize_alias_file_path`)
        // so the name of the master encryption key can be hard-coded.
        const OLD_MASTER_ENCRYPTION_KEY_PATH: &str =
            "/var/lib/iotedge/hsm/enc_keys/edgelet-masterWt5mT2xpO72EPKlt2Tt0Sq4uJCrMvfl2rzzKRB3pnyo_.enc.key";

        let old_master_encryption_key_path = Path::new(OLD_MASTER_ENCRYPTION_KEY_PATH);
        if old_master_encryption_key_path.is_file() {
            Some(old_master_encryption_key_path.to_owned())
        } else {
            None
        }
    };

    let config = execute_inner(old_config_file, old_master_encryption_key_path)?;

    common_config::write_file(new_config_file, &config, &root_user, 0o0600)
        .map_err(|err| format!("{:?}", err))?;

    println!("Azure IoT Edge has been configured successfully!");
    println!(
        "The configuration has been written to {}",
        new_config_file.display()
    );
    println!("To apply the new configuration to services, run:");
    println!();
    println!(
        "    sudo iotedge config apply -c '{}'",
        new_config_file.display()
    );

    Ok(())
}

fn execute_inner(
    old_config_file: &Path,
    old_master_encryption_key_path: Option<PathBuf>,
) -> Result<Vec<u8>, std::borrow::Cow<'static, str>> {
    let old_config_file_display = old_config_file.display();

    let old_config_contents = match std::fs::read_to_string(old_config_file) {
        Ok(old_config) => old_config,
        Err(err) => match err.kind() {
            std::io::ErrorKind::NotFound => {
                return Err(format!(
                    "there is no old config at {} available to migrate",
                    old_config_file_display
                )
                .into())
            }
            _ => return Err(format!("could not open {}: {}", old_config_file_display, err).into()),
        },
    };

    let old_config: old_config::Config = {
        let mut old_config = Config::default();

        old_config
            .merge(YamlFileSource::String(old_config::DEFAULTS.into()))
            .expect("config is not frozen");

        // We use YamlFileSource::String to load the file rather than YamlFileSource::File
        // because config::ConfigError makes it harder to recognize an error from a missing file.
        old_config
            .merge(YamlFileSource::String(old_config_contents.into()))
            .expect("config is not frozen");

        match old_config.try_into() {
            Ok(old_config) => old_config,
            Err(err) => {
                return Err(format!("could not parse {}: {}", old_config_file_display, err).into())
            }
        }
    };

    let old_config::Config {
        provisioning,
        agent,
        hostname,
        parent_hostname,
        connect,
        listen,
        // Ignore the old config's `homedir` value. We want to use a fresh directory and have the right ACLs.
        homedir: _,
        certificates,
        watchdog,
        moby_runtime,
    } = old_config;

    let (provisioning, auto_reprovisioning_mode) = {
        let old_config::Provisioning {
            provisioning,
            dynamic_reprovisioning,
        } = provisioning;

        let (provisioning, always_reprovision_on_startup) = match provisioning {
            old_config::ProvisioningType::Manual(old_config::Manual {
                authentication:
                    old_config::ManualAuthMethod::DeviceConnectionString(
                        old_config::ManualDeviceConnectionString {
                            device_id,
                            hostname,
                            shared_access_key,
                        },
                    ),
            }) => (
                common_config::super_config::Provisioning {
                    provisioning: common_config::super_config::ProvisioningType::Manual {
                        inner: common_config::super_config::ManualProvisioning::Explicit {
                            iothub_hostname: hostname,
                            device_id,
                            authentication:
                                common_config::super_config::ManualAuthMethod::SharedPrivateKey {
                                    device_id_pk:
                                        common_config::super_config::SymmetricKey::Inline {
                                            value: shared_access_key,
                                        },
                                },
                        },
                    },
                },
                false,
            ),

            old_config::ProvisioningType::Manual(old_config::Manual {
                authentication:
                    old_config::ManualAuthMethod::X509(old_config::ManualX509Auth {
                        iothub_hostname,
                        device_id,
                        identity_cert,
                        identity_pk,
                    }),
            }) => (
                common_config::super_config::Provisioning {
                    provisioning: common_config::super_config::ProvisioningType::Manual {
                        inner: common_config::super_config::ManualProvisioning::Explicit {
                            iothub_hostname,
                            device_id,
                            authentication: common_config::super_config::ManualAuthMethod::X509 {
                                identity: common_config::super_config::X509Identity::Preloaded {
                                    identity_cert,
                                    identity_pk: {
                                        let identity_pk: aziot_keys_common::PreloadedKeyLocation =
                                        identity_pk.to_string()
                                        .parse()
                                        .map_err(|err| format!("could not parse provisioning.authentication.identity_pk: {}", err))?;
                                        identity_pk
                                    },
                                },
                            },
                        },
                    },
                },
                false,
            ),

            old_config::ProvisioningType::Dps(old_config::Dps {
                global_endpoint,
                scope_id,
                attestation:
                    old_config::AttestationMethod::SymmetricKey(
                        old_config::SymmetricKeyAttestationInfo {
                            registration_id,
                            symmetric_key,
                        },
                    ),
                always_reprovision_on_startup,
            }) => (
                common_config::super_config::Provisioning {
                    provisioning: common_config::super_config::ProvisioningType::Dps {
                        global_endpoint,
                        id_scope: scope_id,
                        attestation:
                            common_config::super_config::DpsAttestationMethod::SymmetricKey {
                                registration_id,
                                symmetric_key: common_config::super_config::SymmetricKey::Inline {
                                    value: symmetric_key,
                                },
                            },
                    },
                },
                always_reprovision_on_startup,
            ),

            old_config::ProvisioningType::Dps(old_config::Dps {
                global_endpoint,
                scope_id,
                attestation:
                    old_config::AttestationMethod::X509(old_config::X509AttestationInfo {
                        registration_id,
                        identity_cert,
                        identity_pk,
                    }),
                always_reprovision_on_startup,
            }) => (
                common_config::super_config::Provisioning {
                    provisioning: common_config::super_config::ProvisioningType::Dps {
                        global_endpoint,
                        id_scope: scope_id,
                        attestation: common_config::super_config::DpsAttestationMethod::X509 {
                            registration_id,
                            identity: common_config::super_config::X509Identity::Preloaded {
                                identity_cert,
                                identity_pk: {
                                    let identity_pk: aziot_keys_common::PreloadedKeyLocation =
                                        identity_pk.to_string()
                                        .parse()
                                        .map_err(|err| format!("could not parse provisioning.attestation.identity_pk: {}", err))?;
                                    identity_pk
                                },
                            },
                        },
                    },
                },
                always_reprovision_on_startup,
            ),

            old_config::ProvisioningType::Dps(old_config::Dps {
                global_endpoint,
                scope_id,
                attestation:
                    old_config::AttestationMethod::Tpm(old_config::TpmAttestationInfo {
                        registration_id,
                    }),
                always_reprovision_on_startup,
            }) => (
                common_config::super_config::Provisioning {
                    provisioning: common_config::super_config::ProvisioningType::Dps {
                        global_endpoint,
                        id_scope: scope_id,
                        attestation: common_config::super_config::DpsAttestationMethod::Tpm {
                            registration_id,
                        },
                    },
                },
                always_reprovision_on_startup,
            ),

            old_config::ProvisioningType::External(_) => {
                return Err("external provisioning is not supported.".into())
            }
        };

        // When both 'dynamic reprovisioning' and 'always reprovision on startup' settings
        // are set in the old configuration, 'dynamic reprovisioning' is prioritized,
        // since it also triggers a reprovisioning before restarting the daemon.

        let auto_reprovisioning_mode = if dynamic_reprovisioning {
            edgelet_settings::aziot::AutoReprovisioningMode::Dynamic
        } else if always_reprovision_on_startup {
            edgelet_settings::aziot::AutoReprovisioningMode::AlwaysOnStartup
        } else {
            edgelet_settings::aziot::AutoReprovisioningMode::OnErrorOnly
        };

        (provisioning, auto_reprovisioning_mode)
    };

    let (edge_ca, trust_bundle_cert) = {
        if let Some(old_config::Certificates {
            device_cert,
            auto_generated_ca_lifetime_days,
        }) = certificates
        {
            if let Some(old_config::DeviceCertificate {
                device_ca_cert,
                device_ca_pk,
                trusted_ca_certs,
            }) = device_cert
            {
                (
                    Some(super_config::EdgeCa::Preloaded {
                        cert: device_ca_cert,
                        pk: device_ca_pk,
                    }),
                    Some(trusted_ca_certs),
                )
            } else {
                (
                    Some(super_config::EdgeCa::Quickstart {
                        auto_generated_edge_ca_expiry_days: auto_generated_ca_lifetime_days.into(),
                    }),
                    None,
                )
            }
        } else {
            (None, None)
        }
    };

    let config = super_config::Config {
        allow_elevated_docker_permissions: None,

        trust_bundle_cert,

        auto_reprovisioning_mode,

        imported_master_encryption_key: old_master_encryption_key_path,

        manifest_trust_bundle_cert: None,

        aziot: common_config::super_config::Config {
            hostname: Some(hostname),
            parent_hostname,

            provisioning,

            localid: None,

            cloud_timeout_sec: aziot_identityd_config::Settings::default_cloud_timeout(),

            cloud_retries: aziot_identityd_config::Settings::default_cloud_retries(),

            dps_trust_bundle: aziot_identityd_config::Settings::default_dps_trust_bundle(),

            aziot_keys: Default::default(),

            preloaded_keys: Default::default(),

            cert_issuance: Default::default(),

            preloaded_certs: Default::default(),

            endpoints: Default::default(),
        },

        agent: {
            let old_config::ModuleSpec {
                name,
                type_,
                config,
                env,
                image_pull_policy,
            } = agent;
            edgelet_settings::ModuleSpec::new(
                name,
                type_,
                {
                    let old_config::DockerConfig {
                        image,
                        image_id,
                        create_options,
                        digest,
                        auth,
                    } = config;
                    let new_config = edgelet_settings::DockerConfig::new(
                        image,
                        create_options,
                        digest,
                        auth,
                        true,
                    )?;

                    // Clippy wants map_or here, but that's not possible without cloning.
                    #[allow(clippy::option_if_let_else)]
                    if let Some(image_id) = image_id {
                        new_config.with_image_hash(image_id)
                    } else {
                        new_config
                    }
                },
                env,
                match image_pull_policy {
                    old_config::ImagePullPolicy::OnCreate => {
                        edgelet_settings::module::ImagePullPolicy::OnCreate
                    }
                    old_config::ImagePullPolicy::Never => {
                        edgelet_settings::module::ImagePullPolicy::Never
                    }
                },
            )?
        },

        connect: {
            let old_config::Connect {
                management_uri,
                workload_uri,
            } = connect;
            edgelet_settings::uri::Connect {
                workload_uri,
                management_uri,
            }
        },
        listen: {
            fn map_listen_uri(uri: url::Url) -> Result<url::Url, url::Url> {
                if uri.scheme() == "fd" {
                    match uri.host_str() {
                        Some(old_config::DEFAULT_MGMT_SOCKET_UNIT) => {
                            Ok(AZIOT_EDGED_LISTEN_MGMT_SOCKET_ACTIVATED_URI
                                .parse()
                                .expect("hard-coded URI must parse successfully"))
                        }
                        Some(old_config::DEFAULT_WORKLOAD_SOCKET_UNIT) => {
                            Ok(AZIOT_EDGED_LISTEN_WORKLOAD_SOCKET_ACTIVATED_URI
                                .parse()
                                .expect("hard-coded URI must parse successfully"))
                        }
                        _ => Err(uri),
                    }
                } else {
                    Ok(uri)
                }
            }

            let old_config::Listen {
                management_uri,
                workload_uri,
                min_tls_version,
            } = listen;

            let management_uri = map_listen_uri(management_uri).map_err(|management_uri| {
                format!(
                    "unexpected value of listen.management_uri {}",
                    management_uri
                )
            })?;
            let workload_uri = map_listen_uri(workload_uri).map_err(|workload_uri| {
                format!("unexpected value of listen.workload_uri {}", workload_uri)
            })?;

            edgelet_settings::uri::Listen {
                management_uri,
                workload_uri,
                min_tls_version: match min_tls_version {
                    old_config::Protocol::Tls10 => edgelet_settings::uri::MinTlsVersion::Tls10,
                    old_config::Protocol::Tls11 => edgelet_settings::uri::MinTlsVersion::Tls11,
                    old_config::Protocol::Tls12 => edgelet_settings::uri::MinTlsVersion::Tls12,
                },
            }
        },

        watchdog: {
            let old_config::WatchdogSettings { max_retries } = watchdog;
            edgelet_settings::watchdog::Settings {
                max_retries: match max_retries {
                    old_config::RetryLimit::Infinite => {
                        edgelet_settings::watchdog::MaxRetries::Infinite
                    }
                    old_config::RetryLimit::Num(num) => {
                        edgelet_settings::watchdog::MaxRetries::Num(num)
                    }
                },
            }
        },

        edge_ca,

        moby_runtime: {
            let old_config::MobyRuntime {
                uri,
                network,
                content_trust,
            } = moby_runtime;
            super_config::MobyRuntime {
                uri,

                network: match network {
                    old_config::MobyNetwork::Network(network) => {
                        edgelet_settings::MobyNetwork::Network({
                            let old_config::Network { name, ipv6, ipam } = network;
                            edgelet_settings::docker::network::Network {
                                name,
                                ipv6,
                                ipam: ipam.map(|ipam| {
                                    let old_config::Ipam { config } = ipam;
                                    edgelet_settings::Ipam {
                                        config: config.map(|config| {
                                            config
                                                .into_iter()
                                                .map(|config| {
                                                    let old_config::IpamConfig {
                                                        gateway,
                                                        subnet,
                                                        ip_range,
                                                    } = config;
                                                    edgelet_settings::docker::network::IpamConfig {
                                                        gateway,
                                                        subnet,
                                                        ip_range,
                                                    }
                                                })
                                                .collect()
                                        }),
                                    }
                                }),
                            }
                        })
                    }

                    old_config::MobyNetwork::Name(name) => {
                        edgelet_settings::MobyNetwork::Name(name)
                    }
                },

                content_trust: content_trust
                    .map(
                        |content_trust| -> Result<_, std::borrow::Cow<'static, str>> {
                            let old_config::ContentTrust { ca_certs } = content_trust;

                            Ok(super_config::ContentTrust {
                                ca_certs: ca_certs
                                    .map(|ca_certs| -> Result<_, std::borrow::Cow<'static, str>> {
                                        let mut new_ca_certs: std::collections::BTreeMap<_, _> =
                                            Default::default();

                                        for (hostname, cert_path) in ca_certs {
                                            let cert_uri = url::Url::from_file_path(&cert_path)
                                                .map_err(|()| {
                                                    format!(
                                                        "could not convert path {} to file URI",
                                                        cert_path.display()
                                                    )
                                                })?;
                                            new_ca_certs.insert(hostname, cert_uri);
                                        }

                                        Ok(new_ca_certs)
                                    })
                                    .transpose()?,
                            })
                        },
                    )
                    .transpose()?,
            }
        },
    };

    let config =
        toml::to_vec(&config).map_err(|err| format!("could not serialize config: {}", err))?;

    Ok(config)
}

#[cfg(test)]
mod tests {
    #[test]
    fn test() {
        let files_directory =
            std::path::Path::new(concat!(env!("CARGO_MANIFEST_DIR"), "/test-files/config"));
        for entry in std::fs::read_dir(files_directory).unwrap() {
            let entry = entry.unwrap();
            if !entry.file_type().unwrap().is_dir() {
                continue;
            }

            let case_directory = entry.path();

            let test_name = case_directory.file_name().unwrap().to_str().unwrap();

            println!(".\n.\n=========\n.\nRunning test {}", test_name);

            let old_config_file = case_directory.join("old-config.yaml");
            if !old_config_file.exists() {
                // apply-specific test
                continue;
            }

            let expected_config = std::fs::read(case_directory.join("super-config.toml")).unwrap();

            let old_master_encryption_key_path = {
                let expected_preloaded_master_encryption_key_path =
                    case_directory.join("master-encryption-key");
                if expected_preloaded_master_encryption_key_path.exists() {
                    Some("/tmp/master-encryption-key".into())
                } else {
                    None
                }
            };

            let actual_config =
                super::execute_inner(&old_config_file, old_master_encryption_key_path).unwrap();

            // Convert the file contents to bytes::Bytes before asserting, because bytes::Bytes's Debug format
            // prints human-readable strings instead of raw u8s.
            assert_eq!(
                bytes::Bytes::from(expected_config),
                bytes::Bytes::from(actual_config),
                "config does not match"
            );
        }
    }
}
