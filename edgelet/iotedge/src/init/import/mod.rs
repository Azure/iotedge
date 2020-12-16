// Copyright (c) Microsoft. All rights reserved.

mod old_config;

use std::path::Path;

use config::Config;

use edgelet_utils::YamlFileSource;

const AZIOT_KEYD_HOMEDIR_PATH: &str = "/var/lib/aziot/keyd";
const AZIOT_CERTD_HOMEDIR_PATH: &str = "/var/lib/aziot/certd";
const AZIOT_IDENTITYD_HOMEDIR_PATH: &str = "/var/lib/aziot/identityd";
const AZIOT_EDGED_HOMEDIR_PATH: &str = "/var/lib/aziot/edged";

/// The ID used for the device ID key (symmetric or X.509 private) and the device ID cert.
const DEVICE_ID_ID: &str = "device-id";

/// The ID used for the device CA X.509 private key and the device CA cert.
const DEVICE_CA_ID: &str = "device-ca";

/// The ID used for the trust bundle certs file.
const TRUST_BUNDLE_ID: &str = "trust-bundle";

/// The default value of the edged connect and listen management URIs.
const AZIOT_EDGED_MANAGEMENT_URI: &str = "unix:///var/lib/aziot/edged/aziot-edged.mgmt.sock";

/// The default value of the edged connect and listen workload URIs.
const AZIOT_EDGED_WORKLOAD_URI: &str = "unix:///var/lib/aziot/edged/aziot-edged.workload.sock";

pub fn execute(old_config_file: &Path) -> Result<(), std::borrow::Cow<'static, str>> {
    // In production, running as root is the easiest way to guarantee the tool has write access to every service's config file.
    // But it's convenient to not do this for the sake of development because the the development machine doesn't necessarily
    // have the package installed and the users created, and it's easier to have the config files owned by the current user anyway.
    //
    // So when running as root, get the three users appropriately.
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

    // Ignore the old config's `homedir`, `connect` and `listen` values and use the new edged defaults.
    // We want to use a fresh directory and have the right ACLs.
    let old_config::Config {
        provisioning,
        agent,
        hostname,
        parent_hostname,
        connect: _,
        listen: _,
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
            dynamic_reprovisioning,
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
                    dynamic_reprovisioning: *dynamic_reprovisioning,
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
                    dynamic_reprovisioning: *dynamic_reprovisioning,
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
                // TODO: Start migrating this when IS adds support for this flag
                always_reprovision_on_startup: _,
            }) => (
                aziot_identityd_config::Provisioning {
                    dynamic_reprovisioning: *dynamic_reprovisioning,
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
                // TODO: Start migrating this when IS adds support for this flag
                always_reprovision_on_startup: _,
            }) => (
                aziot_identityd_config::Provisioning {
                    dynamic_reprovisioning: *dynamic_reprovisioning,
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
                // TODO: Start migrating this when IS adds support for this flag
                always_reprovision_on_startup: _,
            }) => (
                aziot_identityd_config::Provisioning {
                    dynamic_reprovisioning: *dynamic_reprovisioning,
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
                keyd_config
                    .preloaded_keys
                    .insert(DEVICE_CA_ID.to_owned(), device_ca_pk.to_string());
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
                    DEVICE_CA_ID.to_owned(),
                    aziot_certd_config::PreloadedCert::Uri(device_ca_cert),
                );

                let trusted_ca_certs =
                    file_uri_or_path_to_file_uri(trusted_ca_certs).map_err(|err| {
                        format!("Could not parse certificates.trusted_ca_certs: {}", err)
                    })?;
                certd_config.preloaded_certs.insert(
                    TRUST_BUNDLE_ID.to_owned(),
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
        principal: vec![aziot_identityd_config::Principal {
            uid: aziot_identityd_config::Uid(iotedge_user.uid.as_raw()),
            name: aziot_identity_common::ModuleId("aziot-edge".to_owned()),
            id_type: Some(vec![aziot_identity_common::IdType::Device]),
            localid: None,
        }],
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

            connect: edgelet_core::Connect {
                workload_uri: AZIOT_EDGED_WORKLOAD_URI
                    .parse()
                    .expect("hard-coded URI should parse successfully"),
                management_uri: AZIOT_EDGED_MANAGEMENT_URI
                    .parse()
                    .expect("hard-coded URI should parse successfully"),
            },
            listen: edgelet_core::Listen {
                workload_uri: AZIOT_EDGED_WORKLOAD_URI
                    .parse()
                    .expect("hard-coded URI should parse successfully"),
                management_uri: AZIOT_EDGED_MANAGEMENT_URI
                    .parse()
                    .expect("hard-coded URI should parse successfully"),
                min_tls_version: Default::default(),
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
                        ca_certs: ca_certs.clone(),
                    }
                }),
            }
        },
    };

    if let Some(preloaded_device_id_pk_bytes) = preloaded_device_id_pk_bytes {
        eprintln!("Note: Symmetric key will be written to /var/secrets/aziot/keyd/device-id");

        create_dir_all("/var/secrets/aziot/keyd", &aziotks_user, 0o0700)?;
        write_file(
            "/var/secrets/aziot/keyd/device-id",
            &preloaded_device_id_pk_bytes,
            &aziotks_user,
            0o0600,
        )?;
    }

    let keyd_config = toml::to_vec(&keyd_config)
        .map_err(|err| format!("could not serialize aziot-keyd config: {}", err))?;
    let certd_config = toml::to_vec(&certd_config)
        .map_err(|err| format!("could not serialize aziot-certd config: {}", err))?;
    let tpmd_config = toml::to_vec(&tpmd_config)
        .map_err(|err| format!("could not serialize aziot-certd config: {}", err))?;
    let identityd_config = toml::to_vec(&identityd_config)
        .map_err(|err| format!("could not serialize aziot-identityd config: {}", err))?;
    let edged_config = serde_yaml::to_vec(&edged_config)
        .map_err(|err| format!("could not serialize aziot-edged config: {}", err))?;

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

    eprintln!("aziot-edged has been configured successfully!");
    eprintln!("You can find the configured files at /etc/aziot/{{key,cert,identity,tpm,edge}}d/config.toml");

    Ok(())
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
