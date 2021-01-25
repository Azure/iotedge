// Copyright (c) Microsoft. All rights reserved.

//! This subcommand imports an iotedged config (v1.1 and below) and creates the config files for the five v1.2+ services based on that information.
//!
//! Notes:
//!
//! - Provisioning with a symmetric key (manual or DPS) requires the key to be preloaded into KS, which means it needs to be
//!   saved to a file. This subcommand uses a file named `/var/secrets/aziot/keyd/device-id` for that purpose.
//!   It creates the directory structure and ACLs the directory and the file appropriately to the KS user.
//!
//! - This implementation assumes that Microsoft's implementation of libaziot-keys is being used, in that it generates the keyd config
//!   with the `aziot_keys.homedir_path` property set, and with validation that the preloaded keys must be file paths or `file://` URIs.
//!
//! - PKCS#11 is not set up since iotedged did not support it. The user needs to configure it manually, which includes
//!   configuring their PKCS#11 hardware and library as well as transferring any filesystem keys to the hardware as they want.

mod old_config;

use std::path::Path;

use config::Config;

use edgelet_core::{AZIOT_EDGED_CA_ALIAS, TRUST_BUNDLE_ALIAS};
use edgelet_utils::YamlFileSource;

const AZIOT_KEYD_HOMEDIR_PATH: &str = "/var/lib/aziot/keyd";
const AZIOT_CERTD_HOMEDIR_PATH: &str = "/var/lib/aziot/certd";
const AZIOT_IDENTITYD_HOMEDIR_PATH: &str = "/var/lib/aziot/identityd";
const AZIOT_EDGED_HOMEDIR_PATH: &str = "/var/lib/aziot/edged";

/// The ID used for the device ID key (symmetric or X.509 private) and the device ID cert.
const DEVICE_ID_ID: &str = "device-id";

pub fn execute(old_config_file: &Path) -> Result<(), std::borrow::Cow<'static, str>> {
    // In production, running as root is the easiest way to guarantee the tool has write access to every service's config file.
    // But it's convenient to not do this for the sake of development because the the development machine doesn't necessarily
    // have the package installed and the users created, and it's easier to have the config files owned by the current user anyway.
    //
    // So when running as root, get the five users appropriately.
    // Otherwise, if this is a debug build, fall back to using the current user.
    // Otherwise, tell the user to re-run as root.
    let (aziotks_user, aziotcs_user, aziotid_user, aziottpm_user, iotedge_user) =
        if nix::unistd::Uid::current().is_root() {
            let aziotks_user = nix::unistd::User::from_name("aziotks")
                .map_err(|err| format!("could not query aziotks user information: {}", err))?
                .ok_or("could not query aziotks user information")?;

            let aziotcs_user = nix::unistd::User::from_name("aziotcs")
                .map_err(|err| format!("could not query aziotcs user information: {}", err))?
                .ok_or("could not query aziotcs user information")?;

            let aziotid_user = nix::unistd::User::from_name("aziotid")
                .map_err(|err| format!("could not query aziotid user information: {}", err))?
                .ok_or("could not query aziotid user information")?;

            let aziottpm_user = nix::unistd::User::from_name("aziottpm")
                .map_err(|err| format!("could not query aziottpm user information: {}", err))?
                .ok_or("could not query aziottpm user information")?;

            let iotedge_user = nix::unistd::User::from_name("iotedge")
                .map_err(|err| format!("could not query iotedge user information: {}", err))?
                .ok_or("could not query iotedge user information")?;

            (
                aziotks_user,
                aziotcs_user,
                aziotid_user,
                aziottpm_user,
                iotedge_user,
            )
        } else if cfg!(debug_assertions) {
            let current_user = nix::unistd::User::from_uid(nix::unistd::Uid::current())
                .map_err(|err| format!("could not query current user information: {}", err))?
                .ok_or("could not query current user information")?;
            (
                current_user.clone(),
                current_user.clone(),
                current_user.clone(),
                current_user.clone(),
                current_user,
            )
        } else {
            return Err("this command must be run as root".into());
        };

    for &f in &[
        "/etc/aziot/certd/config.toml",
        "/etc/aziot/edged/config.yaml",
        "/etc/aziot/identityd/config.toml",
        "/etc/aziot/identityd/config.d/aziot-edged.toml",
        "/etc/aziot/keyd/config.toml",
        "/etc/aziot/tpmd/config.toml",
    ] {
        // Don't overwrite any of the configs if they already exist.
        //
        // It would be less racy to test this right before we're about to overwrite the files, but by then we'll have asked the user
        // all of the questions and it would be a waste to give up.
        if Path::new(f).exists() {
            return Err(format!(
                "\
                File {} already exists. \
                Delete this file (after taking a backup if necessary) before running this command.\
            ",
                f
            )
            .into());
        }
    }

    let RunOutput {
        keyd_config,
        certd_config,
        identityd_config,
        tpmd_config,
        edged_config,
        edged_principal_config,
        preloaded_device_id_pk_bytes,
    } = execute_inner(old_config_file, iotedge_user.uid)?;

    if let Some(preloaded_device_id_pk_bytes) = preloaded_device_id_pk_bytes {
        println!("Note: Symmetric key will be written to /var/secrets/aziot/keyd/device-id");

        create_dir_all("/var/secrets/aziot/keyd", &aziotks_user, 0o0700)?;
        write_file(
            "/var/secrets/aziot/keyd/device-id",
            &preloaded_device_id_pk_bytes,
            &aziotks_user,
            0o0600,
        )?;
    }

    write_file(
        "/etc/aziot/keyd/config.toml",
        &keyd_config,
        &aziotks_user,
        0o0600,
    )?;

    write_file(
        "/etc/aziot/certd/config.toml",
        &certd_config,
        &aziotcs_user,
        0o0600,
    )?;

    write_file(
        "/etc/aziot/identityd/config.toml",
        &identityd_config,
        &aziotid_user,
        0o0600,
    )?;

    write_file(
        "/etc/aziot/tpmd/config.toml",
        &tpmd_config,
        &aziottpm_user,
        0o0600,
    )?;

    write_file(
        "/etc/aziot/edged/config.yaml",
        &edged_config,
        &iotedge_user,
        0o0600,
    )?;

    write_file(
        "/etc/aziot/identityd/config.d/aziot-edged.toml",
        &edged_principal_config,
        &aziotid_user,
        0o0600,
    )?;

    println!("aziot-edged has been configured successfully!");
    println!(
        "You can find the configured files at /etc/aziot/{{key,cert,identity,tpm}}d/config.toml, /etc/aziot/edged/config.yaml and /etc/aziot/identityd/config.d/aziot-edged.toml"
    );

    Ok(())
}

#[derive(Debug)]
struct RunOutput {
    certd_config: Vec<u8>,
    identityd_config: Vec<u8>,
    keyd_config: Vec<u8>,
    tpmd_config: Vec<u8>,
    edged_config: Vec<u8>,
    edged_principal_config: Vec<u8>,
    preloaded_device_id_pk_bytes: Option<Vec<u8>>,
}

fn execute_inner(
    old_config_file: &Path,
    iotedge_uid: nix::unistd::Uid,
) -> Result<RunOutput, std::borrow::Cow<'static, str>> {
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

    // Ignore the old config's `homedir` value and use the new edged default. We want to use a fresh directory and have the right ACLs.
    let old_config::Config {
        provisioning,
        agent,
        hostname,
        parent_hostname,
        connect,
        listen,
        homedir: _,
        certificates,
        watchdog,
        moby_runtime,
    } = &old_config;

    let (
        provisioning,
        preloaded_device_id_pk_uri,
        preloaded_device_id_cert_uri,
        preloaded_device_id_pk_bytes,
    ) = {
        let old_config::Provisioning {
            provisioning,
            // TODO: Migrate this to edged config when support for dynamic reprovisioning is reinstated in edged.
            dynamic_reprovisioning: _,
        } = provisioning;

        match provisioning {
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
                aziot_identityd_config::Provisioning {
                    always_reprovision_on_startup: true,
                    provisioning: aziot_identityd_config::ProvisioningType::Manual {
                        iothub_hostname: hostname.clone(),
                        device_id: device_id.clone(),
                        authentication:
                            aziot_identityd_config::ManualAuthMethod::SharedPrivateKey {
                                device_id_pk: DEVICE_ID_ID.to_owned(),
                            },
                    },
                },
                Some(aziot_keys_common::PreloadedKeyLocation::Filesystem {
                    path: "/var/secrets/aziot/keyd/device-id".into(),
                }),
                None,
                Some(shared_access_key.clone()),
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
                aziot_identityd_config::Provisioning {
                    always_reprovision_on_startup: true,
                    provisioning: aziot_identityd_config::ProvisioningType::Manual {
                        iothub_hostname: iothub_hostname.clone(),
                        device_id: device_id.clone(),
                        authentication: aziot_identityd_config::ManualAuthMethod::X509 {
                            identity_cert: DEVICE_ID_ID.to_owned(),
                            identity_pk: DEVICE_ID_ID.to_owned(),
                        },
                    },
                },
                Some({
                    let identity_pk_uri = identity_pk.to_string().parse().map_err(|err| {
                        format!(
                            "Could not parse provisioning.authentication.identity_pk: {}",
                            err
                        )
                    })?;
                    identity_pk_uri
                }),
                Some(aziot_certd_config::PreloadedCert::Uri(
                    identity_cert.clone(),
                )),
                None,
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
                aziot_identityd_config::Provisioning {
                    always_reprovision_on_startup: *always_reprovision_on_startup,
                    provisioning: aziot_identityd_config::ProvisioningType::Dps {
                        global_endpoint: global_endpoint.to_string(),
                        scope_id: scope_id.clone(),
                        attestation: aziot_identityd_config::DpsAttestationMethod::SymmetricKey {
                            registration_id: registration_id.clone(),
                            symmetric_key: DEVICE_ID_ID.to_owned(),
                        },
                    },
                },
                Some(aziot_keys_common::PreloadedKeyLocation::Filesystem {
                    path: "/var/secrets/aziot/keyd/device-id".into(),
                }),
                None,
                Some(symmetric_key.clone()),
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
                aziot_identityd_config::Provisioning {
                    always_reprovision_on_startup: *always_reprovision_on_startup,
                    provisioning: aziot_identityd_config::ProvisioningType::Dps {
                        global_endpoint: global_endpoint.to_string(),
                        scope_id: scope_id.clone(),
                        attestation: aziot_identityd_config::DpsAttestationMethod::X509 {
                            // TODO: Remove this when IS supports registration ID being optional for DPS-X509
                            registration_id: registration_id
                                .clone()
                                .ok_or_else(|| "registration ID is currently required")?,
                            identity_cert: DEVICE_ID_ID.to_owned(),
                            identity_pk: DEVICE_ID_ID.to_owned(),
                        },
                    },
                },
                Some({
                    let identity_pk_uri = identity_pk.to_string().parse().map_err(|err| {
                        format!(
                            "Could not parse provisioning.authentication.identity_pk: {}",
                            err
                        )
                    })?;
                    identity_pk_uri
                }),
                Some(aziot_certd_config::PreloadedCert::Uri(
                    identity_cert.clone(),
                )),
                None,
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
                aziot_identityd_config::Provisioning {
                    always_reprovision_on_startup: *always_reprovision_on_startup,
                    provisioning: aziot_identityd_config::ProvisioningType::Dps {
                        global_endpoint: global_endpoint.to_string(),
                        scope_id: scope_id.clone(),
                        attestation: aziot_identityd_config::DpsAttestationMethod::Tpm {
                            registration_id: registration_id.clone(),
                        },
                    },
                },
                None,
                None,
                None,
            ),

            old_config::ProvisioningType::External(_) => {
                return Err("external provisioning is not supported.".into())
            }
        }
    };

    let keyd_config = {
        let mut keyd_config = aziot_keyd_config::Config {
            aziot_keys: Default::default(),
            preloaded_keys: Default::default(),
            endpoints: Default::default(),
        };

        keyd_config.aziot_keys.insert(
            "homedir_path".to_owned(),
            AZIOT_KEYD_HOMEDIR_PATH.to_owned(),
        );

        if let Some(preloaded_device_id_pk_uri) = preloaded_device_id_pk_uri {
            keyd_config.preloaded_keys.insert(
                DEVICE_ID_ID.to_owned(),
                preloaded_device_id_pk_uri.to_string(),
            );
        }

        if let Some(old_config::Certificates {
            device_cert,
            auto_generated_ca_lifetime_days: _,
        }) = certificates
        {
            if let Some(old_config::DeviceCertificate {
                device_ca_cert: _,
                device_ca_pk,
                trusted_ca_certs: _,
            }) = device_cert
            {
                let device_ca_pk = file_uri_or_path_to_file_uri(device_ca_pk)
                    .map_err(|err| format!("Could not parse certificates.device_ca_pk: {}", err))?;
                keyd_config.preloaded_keys.insert(
                    edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned(),
                    device_ca_pk.to_string(),
                );
            }
        }

        keyd_config
    };

    let certd_config = {
        let mut certd_config = aziot_certd_config::Config {
            homedir_path: AZIOT_CERTD_HOMEDIR_PATH.into(),
            cert_issuance: Default::default(),
            preloaded_certs: Default::default(),
            endpoints: Default::default(),
        };

        if let Some(preloaded_device_id_cert_uri) = preloaded_device_id_cert_uri {
            certd_config
                .preloaded_certs
                .insert(DEVICE_ID_ID.to_owned(), preloaded_device_id_cert_uri);
        }

        if let Some(old_config::Certificates {
            device_cert,
            auto_generated_ca_lifetime_days: _,
        }) = certificates
        {
            if let Some(old_config::DeviceCertificate {
                device_ca_cert,
                device_ca_pk: _,
                trusted_ca_certs,
            }) = device_cert
            {
                let device_ca_cert =
                    file_uri_or_path_to_file_uri(device_ca_cert).map_err(|err| {
                        format!("Could not parse certificates.device_ca_cert: {}", err)
                    })?;
                certd_config.preloaded_certs.insert(
                    edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned(),
                    aziot_certd_config::PreloadedCert::Uri(device_ca_cert),
                );

                let trusted_ca_certs =
                    file_uri_or_path_to_file_uri(trusted_ca_certs).map_err(|err| {
                        format!("Could not parse certificates.trusted_ca_certs: {}", err)
                    })?;
                certd_config.preloaded_certs.insert(
                    edgelet_core::TRUST_BUNDLE_ALIAS.to_owned(),
                    aziot_certd_config::PreloadedCert::Uri(trusted_ca_certs),
                );
            }
        }

        certd_config
    };

    let tpmd_config = aziot_tpmd_config::Config {
        endpoints: Default::default(),
    };

    let identityd_config = aziot_identityd_config::Settings {
        hostname: hostname.clone(),
        homedir: AZIOT_IDENTITYD_HOMEDIR_PATH.into(),
        principal: vec![],
        provisioning,
        endpoints: Default::default(),
        localid: None,
    };

    let edged_config = edgelet_docker::Settings {
        base: edgelet_core::Settings {
            agent: {
                let old_config::ModuleSpec {
                    name,
                    type_,
                    config,
                    env,
                    image_pull_policy,
                } = agent;
                edgelet_core::ModuleSpec {
                    name: name.clone(),
                    type_: type_.clone(),
                    config: {
                        let old_config::DockerConfig {
                            image,
                            image_id,
                            create_options,
                            digest,
                            auth,
                        } = config;
                        edgelet_docker::DockerConfig {
                            image: image.clone(),
                            image_id: image_id.clone(),
                            create_options: create_options.clone(),
                            digest: digest.clone(),
                            auth: auth.clone(),
                        }
                    },
                    env: env.clone(),
                    image_pull_policy: match image_pull_policy {
                        old_config::ImagePullPolicy::OnCreate => {
                            edgelet_core::ImagePullPolicy::OnCreate
                        }
                        old_config::ImagePullPolicy::Never => edgelet_core::ImagePullPolicy::Never,
                    },
                }
            },

            hostname: hostname.clone(),
            parent_hostname: parent_hostname.clone(),

            connect: {
                let old_config::Connect {
                    workload_uri,
                    management_uri,
                } = connect;
                edgelet_core::Connect {
                    workload_uri: workload_uri.clone(),
                    management_uri: management_uri.clone(),
                }
            },
            listen: {
                let old_config::Listen {
                    workload_uri,
                    management_uri,
                    min_tls_version,
                } = listen;
                edgelet_core::Listen {
                    workload_uri: workload_uri.clone(),
                    management_uri: management_uri.clone(),
                    min_tls_version: match min_tls_version {
                        old_config::Protocol::Tls10 => edgelet_core::Protocol::Tls10,
                        old_config::Protocol::Tls11 => edgelet_core::Protocol::Tls11,
                        old_config::Protocol::Tls12 => edgelet_core::Protocol::Tls12,
                    },
                }
            },

            homedir: AZIOT_EDGED_HOMEDIR_PATH.into(),

            watchdog: {
                let old_config::WatchdogSettings { max_retries } = watchdog;
                edgelet_core::WatchdogSettings {
                    max_retries: match *max_retries {
                        old_config::RetryLimit::Infinite => edgelet_core::RetryLimit::Infinite,
                        old_config::RetryLimit::Num(num) => edgelet_core::RetryLimit::Num(num),
                    },
                }
            },

            endpoints: Default::default(),
            edge_ca_cert: Some(AZIOT_EDGED_CA_ALIAS.to_owned()),
            edge_ca_key: Some(AZIOT_EDGED_CA_ALIAS.to_owned()),
            trust_bundle_cert: Some(TRUST_BUNDLE_ALIAS.to_owned()),
        },

        moby_runtime: {
            let old_config::MobyRuntime {
                uri,
                network,
                content_trust,
            } = moby_runtime;
            edgelet_docker::MobyRuntime {
                uri: uri.clone(),

                network: match network {
                    old_config::MobyNetwork::Network(network) => {
                        edgelet_core::MobyNetwork::Network({
                            let old_config::Network { name, ipv6, ipam } = network;
                            edgelet_core::Network {
                                name: name.clone(),
                                ipv6: *ipv6,
                                ipam: ipam.as_ref().map(|ipam| {
                                    let old_config::Ipam { config } = ipam;
                                    edgelet_core::Ipam {
                                        config: config.as_ref().map(|config| {
                                            config
                                                .iter()
                                                .map(|config| {
                                                    let old_config::IpamConfig {
                                                        gateway,
                                                        subnet,
                                                        ip_range,
                                                    } = config;
                                                    edgelet_core::IpamConfig {
                                                        gateway: gateway.clone(),
                                                        subnet: subnet.clone(),
                                                        ip_range: ip_range.clone(),
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
                        edgelet_core::MobyNetwork::Name(name.clone())
                    }
                },

                content_trust: content_trust.as_ref().map(|content_trust| {
                    let old_config::ContentTrust { ca_certs } = content_trust;
                    edgelet_docker::ContentTrust {
                        ca_certs: ca_certs.as_ref().map(|ca_certs| {
                            ca_certs
                                .iter()
                                .map(|(k, v)| (k.clone(), v.clone()))
                                .collect()
                        }),
                    }
                }),
            }
        },
    };

    let edged_principal_config = aziot_identityd_config::Principal {
        uid: aziot_identityd_config::Uid(iotedge_uid.as_raw()),
        name: aziot_identity_common::ModuleId("aziot-edge".to_owned()),
        id_type: None,
        localid: None,
    };

    let keyd_config = toml::to_vec(&keyd_config)
        .map_err(|err| format!("could not serialize aziot-keyd config: {}", err))?;
    let certd_config = toml::to_vec(&certd_config)
        .map_err(|err| format!("could not serialize aziot-certd config: {}", err))?;
    let tpmd_config = toml::to_vec(&tpmd_config)
        .map_err(|err| format!("could not serialize aziot-certd config: {}", err))?;
    let identityd_config = toml::to_vec(&identityd_config)
        .map_err(|err| format!("could not serialize aziot-identityd config: {}", err))?;
    let edged_config = {
        let mut edged_config = serde_yaml::to_vec(&edged_config)
            .map_err(|err| format!("could not serialize aziot-edged config: {}", err))?;
        // serde_yaml prepends the output with `---\n` and does not end the output with `\n`, so fix both of those things.
        //
        // (The `---\n` doesn't hurt, but it's cleaner to remove it to be consistent with the default config.)
        if edged_config.starts_with(b"---\n") {
            // Would be nice to use `<[u8]>::strip_prefix`, but it's not yet stable.
            edged_config = edged_config.split_off(b"---\n".len());
        }
        edged_config.push(b'\n');
        edged_config
    };
    let edged_principal_config = {
        // There's no way to serialize the `principal` field as a TOML list named `principal`
        // without making a whole new struct with a `principal: Vec<Principal>` field.
        // So just write the header manually.
        let mut edged_principal_config_serialized = b"[[principal]]\n".to_vec();
        edged_principal_config_serialized.extend(
            &toml::to_vec(&edged_principal_config).map_err(|err| {
                format!("could not serialize aziot-edged principal config: {}", err)
            })?,
        );
        edged_principal_config_serialized
    };

    Ok(RunOutput {
        keyd_config,
        certd_config,
        identityd_config,
        tpmd_config,
        edged_config,
        edged_principal_config,
        preloaded_device_id_pk_bytes,
    })
}

fn file_uri_or_path_to_file_uri(value: &str) -> Result<url::Url, String> {
    let value = value
        .parse::<url::Url>()
        .or_else(|err| url::Url::from_file_path(&value).map_err(|()| err));
    match value {
        Ok(value) if value.scheme() == "file" => Ok(value),

        Ok(value) => Err(format!(
            r#"Value has invalid scheme {:?}. Only "file://" URIs are supported."#,
            value.scheme(),
        )),

        Err(err) => Err(format!(
            "Could not parse value as a file path or a file:// URI: {}",
            err,
        )),
    }
}

fn create_dir_all(
    path: &(impl AsRef<std::path::Path> + ?Sized),
    user: &nix::unistd::User,
    mode: u32,
) -> Result<(), String> {
    let path = path.as_ref();
    let path_displayable = path.display();

    let () = std::fs::create_dir_all(path)
        .map_err(|err| format!("could not create {} directory: {}", path_displayable, err))?;
    let () = nix::unistd::chown(path, Some(user.uid), Some(user.gid)).map_err(|err| {
        format!(
            "could not set ownership on {} directory: {}",
            path_displayable, err
        )
    })?;
    let () = std::fs::set_permissions(path, std::os::unix::fs::PermissionsExt::from_mode(mode))
        .map_err(|err| {
            format!(
                "could not set permissions on {} directory: {}",
                path_displayable, err
            )
        })?;
    Ok(())
}

fn write_file(
    path: &(impl AsRef<std::path::Path> + ?Sized),
    content: &[u8],
    user: &nix::unistd::User,
    mode: u32,
) -> Result<(), String> {
    let path = path.as_ref();
    let path_displayable = path.display();

    let () = std::fs::write(path, content)
        .map_err(|err| format!("could not create {}: {}", path_displayable, err))?;
    let () = nix::unistd::chown(path, Some(user.uid), Some(user.gid))
        .map_err(|err| format!("could not set ownership on {}: {}", path_displayable, err))?;
    let () = std::fs::set_permissions(path, std::os::unix::fs::PermissionsExt::from_mode(mode))
        .map_err(|err| format!("could not set permissions on {}: {}", path_displayable, err))?;
    Ok(())
}

#[cfg(test)]
mod tests {
    #[test]
    fn test() {
        let files_directory = std::path::Path::new(concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/test-files/init/import"
        ));
        for entry in std::fs::read_dir(files_directory).unwrap() {
            let entry = entry.unwrap();
            if !entry.file_type().unwrap().is_dir() {
                continue;
            }

            let case_directory = entry.path();

            let test_name = case_directory.file_name().unwrap().to_str().unwrap();

            println!(".\n.\n=========\n.\nRunning test {}", test_name);

            let old_config_file = case_directory.join("input.yaml");
            let expected_keyd_config = std::fs::read(case_directory.join("keyd.toml")).unwrap();
            let expected_certd_config = std::fs::read(case_directory.join("certd.toml")).unwrap();
            let expected_identityd_config =
                std::fs::read(case_directory.join("identityd.toml")).unwrap();
            let expected_tpmd_config = std::fs::read(case_directory.join("tpmd.toml")).unwrap();
            let expected_edged_config = std::fs::read(case_directory.join("edged.yaml")).unwrap();
            let expected_edged_principal_config =
                std::fs::read(case_directory.join("edged-principal.toml")).unwrap();

            let expected_preloaded_device_id_pk_bytes =
                match std::fs::read(case_directory.join("device-id")) {
                    Ok(contents) => Some(contents),
                    Err(err) if err.kind() == std::io::ErrorKind::NotFound => None,
                    Err(err) => panic!("could not read device-id file: {}", err),
                };

            let super::RunOutput {
                keyd_config: actual_keyd_config,
                certd_config: actual_certd_config,
                identityd_config: actual_identityd_config,
                tpmd_config: actual_tpmd_config,
                edged_config: actual_edged_config,
                edged_principal_config: actual_edged_principal_config,
                preloaded_device_id_pk_bytes: actual_preloaded_device_id_pk_bytes,
            } = super::execute_inner(&old_config_file, nix::unistd::Uid::from_raw(5555)).unwrap();

            // Convert the five configs to bytes::Bytes before asserting, because bytes::Bytes's Debug format prints strings.
            // It doesn't matter for the device ID file since it's binary anyway.
            assert_eq!(
                bytes::Bytes::from(expected_keyd_config),
                bytes::Bytes::from(actual_keyd_config),
                "keyd config does not match"
            );
            assert_eq!(
                bytes::Bytes::from(expected_certd_config),
                bytes::Bytes::from(actual_certd_config),
                "certd config does not match"
            );
            assert_eq!(
                bytes::Bytes::from(expected_identityd_config),
                bytes::Bytes::from(actual_identityd_config),
                "identityd config does not match"
            );
            assert_eq!(
                bytes::Bytes::from(expected_tpmd_config),
                bytes::Bytes::from(actual_tpmd_config),
                "tpmd config does not match"
            );
            assert_eq!(
                bytes::Bytes::from(expected_edged_config),
                bytes::Bytes::from(actual_edged_config),
                "edged config does not match"
            );
            assert_eq!(
                bytes::Bytes::from(expected_edged_principal_config),
                bytes::Bytes::from(actual_edged_principal_config),
                "edged config does not match"
            );
            assert_eq!(
                expected_preloaded_device_id_pk_bytes, actual_preloaded_device_id_pk_bytes,
                "device ID key bytes do not match"
            );
        }
    }
}
