// Copyright (c) Microsoft. All rights reserved.

//! This subcommand takes the super-config file, converts it into the individual services' config files,
//! writes those files, and restarts the services.

use std::{collections::HashMap, path::Path};

use aziotctl_common::config as common_config;

use super::super_config;
use docker::{DockerApi, DockerApiClient};
use http_common::Connector;

const AZIOT_EDGED_HOMEDIR_PATH: &str = "/var/lib/aziot/edged";

const TRUST_BUNDLE_USER_ALIAS: &str = "trust-bundle-user";

const LABELS: &[&str] = &["net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent"];

pub async fn execute(config: &Path) -> Result<(), std::borrow::Cow<'static, str>> {
    // In production, running as root is the easiest way to guarantee the tool has write access to every service's config file.
    // But it's convenient to not do this for the sake of development because the the development machine doesn't necessarily
    // have the package installed and the users created, and it's easier to have the config files owned by the current user anyway.
    //
    // So when running as root, get the four users appropriately.
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

    let RunOutput {
        keyd_config,
        certd_config,
        identityd_config,
        tpmd_config,
        edged_config,
        preloaded_device_id_pk_bytes,
        preloaded_master_encryption_key_bytes,
    } = execute_inner(config, aziotcs_user.uid, aziotid_user.uid, iotedge_user.uid).await?;

    if let Some(preloaded_device_id_pk_bytes) = preloaded_device_id_pk_bytes {
        println!("Note: Symmetric key will be written to /var/secrets/aziot/keyd/device-id");

        common_config::create_dir_all("/var/secrets/aziot/keyd", &aziotks_user, 0o0700)
            .map_err(|err| format!("{:?}", err))?;
        common_config::write_file(
            "/var/secrets/aziot/keyd/device-id",
            &preloaded_device_id_pk_bytes,
            &aziotks_user,
            0o0600,
        )
        .map_err(|err| format!("{:?}", err))?;
    }

    if let Some(preloaded_master_encryption_key_bytes) = preloaded_master_encryption_key_bytes {
        println!("Note: Imported master encryption key will be written to /var/secrets/aziot/keyd/imported-master-encryption-key");

        common_config::create_dir_all("/var/secrets/aziot/keyd", &aziotks_user, 0o0700)
            .map_err(|err| format!("{:?}", err))?;
        common_config::write_file(
            "/var/secrets/aziot/keyd/imported-master-encryption-key",
            &preloaded_master_encryption_key_bytes,
            &aziotks_user,
            0o0600,
        )
        .map_err(|err| format!("{:?}", err))?;
    }

    common_config::write_file(
        "/etc/aziot/keyd/config.d/00-super.toml",
        keyd_config.as_bytes(),
        &aziotks_user,
        0o0600,
    )
    .map_err(|err| format!("{:?}", err))?;

    common_config::write_file(
        "/etc/aziot/certd/config.d/00-super.toml",
        certd_config.as_bytes(),
        &aziotcs_user,
        0o0600,
    )
    .map_err(|err| format!("{:?}", err))?;

    common_config::write_file(
        "/etc/aziot/identityd/config.d/00-super.toml",
        identityd_config.as_bytes(),
        &aziotid_user,
        0o0600,
    )
    .map_err(|err| format!("{:?}", err))?;

    common_config::write_file(
        "/etc/aziot/tpmd/config.d/00-super.toml",
        tpmd_config.as_bytes(),
        &aziottpm_user,
        0o0600,
    )
    .map_err(|err| format!("{:?}", err))?;

    common_config::write_file(
        "/etc/aziot/edged/config.d/00-super.toml",
        edged_config.as_bytes(),
        &iotedge_user,
        0o0600,
    )
    .map_err(|err| format!("{:?}", err))?;

    println!("Azure IoT Edge has been configured successfully!");
    println!();
    println!("Restarting service for configuration to take effect...");
    crate::System::system_restart().map_err(|err| format!("{}", err))?;
    println!("Done.");

    Ok(())
}

#[derive(Debug)]
struct RunOutput {
    certd_config: String,
    identityd_config: String,
    keyd_config: String,
    tpmd_config: String,
    edged_config: String,
    preloaded_device_id_pk_bytes: Option<Vec<u8>>,
    preloaded_master_encryption_key_bytes: Option<Vec<u8>>,
}

async fn execute_inner(
    config: &std::path::Path,
    aziotcs_uid: nix::unistd::Uid,
    aziotid_uid: nix::unistd::Uid,
    iotedge_uid: nix::unistd::Uid,
) -> Result<RunOutput, std::borrow::Cow<'static, str>> {
    let config = std::fs::read(config)
        .map_err(|err| format!("could not read config file {}: {}", config.display(), err))?;
    let config =
        std::str::from_utf8(&config).map_err(|err| format!("error parsing config: {}", err))?;

    let super_config::Config {
        trust_bundle_cert,
        allow_elevated_docker_permissions,
        auto_reprovisioning_mode,
        imported_master_encryption_key,
        #[cfg(contenttrust)]
            manifest_trust_bundle_cert: _,
        additional_info,
        iotedge_max_requests,
        aziot,
        agent,
        connect,
        listen,
        watchdog,
        edge_ca,
        moby_runtime,
        image_garbage_collection,
    } = toml::from_str(config).map_err(|err| format!("could not parse config file: {}", err))?;

    let aziotctl_common::config::apply::RunOutput {
        mut certd_config,
        mut identityd_config,
        mut keyd_config,
        tpmd_config,
        preloaded_device_id_pk_bytes,
    } = aziotctl_common::config::apply::run(aziot, aziotcs_uid, aziotid_uid)
        .map_err(|err| format!("{:?}", err))?;

    let old_identityd_path = Path::new("/etc/aziot/identityd/config.d/00-super.toml");
    if let Ok(old_identity_config) = std::fs::read(old_identityd_path) {
        let old_identity_config = std::str::from_utf8(&old_identity_config)
            .map_err(|err| format!("error parsing config: {}", err))?;

        if let Ok(aziot_identityd_config::Settings { hostname, .. }) =
            toml::from_str(old_identity_config)
        {
            let new_hostname = &identityd_config.hostname;
            let moby_runtime = &moby_runtime;
            let uri = &moby_runtime.uri;

            let client = DockerApiClient::new(
                Connector::new(uri)
                    .map_err(|err| format!("Failed to make docker client: {}", err))?,
            );

            let mut filters = HashMap::new();
            filters.insert("label", LABELS);
            let filters = serde_json::to_string(&filters).map_err(|err| format!("{:?}", err))?;

            let containers = client
                .container_list(
                    true,  /*all*/
                    0,     /*limit*/
                    false, /*size*/
                    &filters,
                )
                .await
                .map_err(|err| format!("{:?}", err))?;
            if &hostname != new_hostname && !containers.is_empty() {
                return Err(format!("Cannot apply config because the hostname in the config {} is different from the previous hostname {}. To update the hostname, run the following command which deletes all IoT Edge modules and reapplies the configuration. Or, revert the hostname change in the config.toml file.
                    sudo iotedge system stop && sudo docker rm -f $(sudo docker ps -aq -f \"label=net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent\") && sudo iotedge config apply
                Warning: Data stored in the modules is lost when above command is executed.", &hostname, &new_hostname).into());
            }
        } else {
            println!("Warning: the previous identity config file is unreadable");
        }
    } else {
        println!("Warning: the previous identity config file is unreadable");
    }
    let mut iotedge_authorized_certs = vec![
        edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned(),
        "aziot-edged/module/*".to_owned(),
    ];

    let mut iotedge_authorized_keys = vec![
        edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned(),
        "iotedge_master_encryption_id".to_owned(),
    ];

    identityd_config
        .principal
        .push(aziot_identityd_config::Principal {
            uid: aziot_identityd_config::Uid(iotedge_uid.as_raw()),
            name: aziot_identity_common::ModuleId("aziot-edge".to_owned()),
            id_type: None,
            localid: None,
        });

    let preloaded_master_encryption_key_bytes = {
        if let Some(imported_master_encryption_key) = imported_master_encryption_key {
            let preloaded_master_encryption_key_bytes =
                std::fs::read(&imported_master_encryption_key).map_err(|err| {
                    format!(
                        "could not import master encryption key file {}: {}",
                        imported_master_encryption_key.display(),
                        err
                    )
                })?;
            keyd_config.preloaded_keys.insert(
                "iotedge_master_encryption_id".to_owned(),
                (aziot_keys_common::PreloadedKeyLocation::Filesystem {
                    path: "/var/secrets/aziot/keyd/imported-master-encryption-key".into(),
                })
                .to_string(),
            );
            Some(preloaded_master_encryption_key_bytes)
        } else {
            None
        }
    };

    let mut trust_bundle_certs = vec![edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()];

    let edge_ca = edge_ca.unwrap_or(super_config::EdgeCa::Quickstart {
        auto_generated_edge_ca_expiry_days: 90,
        auto_renew: cert_renewal::AutoRenewConfig::default(),
        subject: None,
    });

    let edge_ca_config = match edge_ca {
        super_config::EdgeCa::Issued { cert } => {
            match cert.method {
                common_config::super_config::CertIssuanceMethod::Est { url, auth } => {
                    let mut aziotcs_principal = aziot_keyd_config::Principal {
                        uid: aziotcs_uid.as_raw(),
                        keys: vec![edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()],
                    };

                    let mut keys = std::collections::BTreeMap::default();

                    let auth = common_config::apply::set_est_auth(
                        auth.as_ref(),
                        &mut certd_config.preloaded_certs,
                        &mut keys,
                        &mut aziotcs_principal,
                        edgelet_settings::AZIOT_EDGED_CA_ALIAS,
                    );

                    keyd_config.principal.push(aziotcs_principal);

                    keyd_config.preloaded_keys.append(
                        &mut keys
                            .into_iter()
                            .map(|(id, location)| (id, location.to_string()))
                            .collect(),
                    );

                    let issuance = aziot_certd_config::CertIssuanceOptions {
                        method: aziot_certd_config::CertIssuanceMethod::Est { url, auth },

                        // Ignore expiry_days set in the super config. There's no place for it in an
                        // EST request.
                        expiry_days: None,

                        // The subject will be used by Edge daemon to generate the CSR. Ignore it in
                        // certd's settings; certd cannot modify the CSR anyways.
                        subject: None,
                    };

                    certd_config.cert_issuance.certs.insert(
                        edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned(),
                        issuance.clone(),
                    );

                    let auto_renew = cert.auto_renew.unwrap_or_default();
                    let temp_cert = format!("{}-temp", edgelet_settings::AZIOT_EDGED_CA_ALIAS);
                    certd_config.cert_issuance.certs.insert(temp_cert, issuance);

                    edgelet_settings::base::EdgeCa {
                        cert: Some(edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()),
                        key: Some(edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()),
                        auto_renew: Some(auto_renew),
                        subject: cert.subject,
                    }
                }
                common_config::super_config::CertIssuanceMethod::LocalCa => {
                    let issuance = aziot_certd_config::CertIssuanceOptions {
                        method: aziot_certd_config::CertIssuanceMethod::LocalCa,
                        expiry_days: cert.expiry_days,
                        subject: cert.subject,
                    };

                    certd_config.cert_issuance.certs.insert(
                        edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned(),
                        issuance.clone(),
                    );

                    let auto_renew = cert.auto_renew.unwrap_or_default();
                    let temp_cert = format!("{}-temp", edgelet_settings::AZIOT_EDGED_CA_ALIAS);
                    certd_config.cert_issuance.certs.insert(temp_cert, issuance);

                    keyd_config.principal.push(aziot_keyd_config::Principal {
                        uid: aziotcs_uid.as_raw(),
                        keys: vec![edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()],
                    });

                    edgelet_settings::base::EdgeCa {
                        cert: Some(edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()),
                        key: Some(edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()),
                        auto_renew: Some(auto_renew),

                        // The cert subject override is set in certd's settings and does not need
                        // to be set in Edge daemon.
                        subject: None,
                    }
                }
                common_config::super_config::CertIssuanceMethod::SelfSigned => {
                    // This is equivalent to a quickstart CA.
                    set_quickstart_ca(
                        &mut keyd_config,
                        &mut certd_config,
                        aziotcs_uid,
                        cert.expiry_days,
                        cert.subject,
                    );

                    let auto_renew = cert.auto_renew.unwrap_or_default();

                    edgelet_settings::base::EdgeCa {
                        cert: Some(edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()),
                        key: Some(edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()),
                        auto_renew: Some(auto_renew),

                        // The cert subject override is set in certd's settings and does not need
                        // to be set in Edge daemon.
                        subject: None,
                    }
                }
            }
        }
        super_config::EdgeCa::Preloaded { cert, pk } => {
            keyd_config.preloaded_keys.insert(
                edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned(),
                pk.to_string(),
            );

            certd_config.preloaded_certs.insert(
                edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned(),
                aziot_certd_config::PreloadedCert::Uri(cert),
            );

            edgelet_settings::base::EdgeCa {
                cert: Some(edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()),
                key: Some(edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()),
                auto_renew: None,
                subject: None,
            }
        }
        super_config::EdgeCa::Quickstart {
            auto_generated_edge_ca_expiry_days,
            auto_renew,
            subject,
        } => {
            set_quickstart_ca(
                &mut keyd_config,
                &mut certd_config,
                aziotcs_uid,
                Some(auto_generated_edge_ca_expiry_days),
                subject,
            );

            edgelet_settings::base::EdgeCa {
                cert: None,
                key: None,
                auto_renew: Some(auto_renew),
                subject: None,
            }
        }
    };

    // Edge daemon needs authorization to manage temporary credentials for Edge CA renewal.
    if let Some(auto_renew) = &edge_ca_config.auto_renew {
        let temp = format!("{}-temp", edgelet_settings::AZIOT_EDGED_CA_ALIAS);

        iotedge_authorized_certs.push(temp.clone());

        if auto_renew.rotate_key {
            iotedge_authorized_keys.push(temp);
        }
    }

    certd_config.principal.push(aziot_certd_config::Principal {
        uid: iotedge_uid.as_raw(),
        certs: iotedge_authorized_certs,
    });

    keyd_config.principal.push(aziot_keyd_config::Principal {
        uid: iotedge_uid.as_raw(),
        keys: iotedge_authorized_keys,
    });

    if let Some(trust_bundle_cert) = trust_bundle_cert {
        certd_config.preloaded_certs.insert(
            TRUST_BUNDLE_USER_ALIAS.to_owned(),
            aziot_certd_config::PreloadedCert::Uri(trust_bundle_cert),
        );

        trust_bundle_certs.push(TRUST_BUNDLE_USER_ALIAS.to_owned());
    }

    certd_config.preloaded_certs.insert(
        edgelet_settings::TRUST_BUNDLE_ALIAS.to_owned(),
        aziot_certd_config::PreloadedCert::Ids(trust_bundle_certs),
    );

    let manifest_trust_bundle_cert = None;

    let additional_info = if let Some(additional_info) = additional_info {
        let scheme = additional_info.scheme();
        if scheme != "file" {
            return Err(format!("unsupported additional_info scheme: {}", scheme).into());
        }
        let path = additional_info
            .to_file_path()
            .map_err(|_| "additional_info is an invalid URI")?;
        let lossy = path.to_string_lossy();
        let bytes = std::fs::read(&path)
            .map_err(|e| format!("failed to read additional_info from {}: {:?}", lossy, e))?;
        let bytes = std::str::from_utf8(&bytes)
            .map_err(|e| format!("failed to parse additional_info: {}", e))?;
        toml::de::from_str(bytes).map_err(|e| format!("invalid toml at {}: {:?}", lossy, e))?
    } else {
        std::collections::BTreeMap::new()
    };

    let edged_config = edgelet_settings::Settings {
        base: edgelet_settings::base::Settings {
            hostname: identityd_config.hostname.clone(),

            edge_ca: edge_ca_config,
            trust_bundle_cert: Some(edgelet_settings::TRUST_BUNDLE_ALIAS.to_owned()),
            manifest_trust_bundle_cert,
            additional_info,

            auto_reprovisioning_mode,

            homedir: AZIOT_EDGED_HOMEDIR_PATH.into(),

            allow_elevated_docker_permissions: allow_elevated_docker_permissions.unwrap_or(true),

            iotedge_max_requests,

            agent,

            connect,
            listen,

            watchdog,

            endpoints: Default::default(),

            image_garbage_collection,
        },

        moby_runtime: {
            let super_config::MobyRuntime {
                uri,
                network,
                content_trust,
            } = moby_runtime;

            edgelet_settings::MobyRuntime {
                uri,
                network,
                content_trust: content_trust
                    .map(
                        |content_trust| -> Result<_, std::borrow::Cow<'static, str>> {
                            let super_config::ContentTrust { ca_certs } = content_trust;

                            Ok(edgelet_settings::ContentTrust {
                                ca_certs: ca_certs
                                    .map(|ca_certs| -> Result<_, std::borrow::Cow<'static, str>> {
                                        let mut new_ca_certs: std::collections::BTreeMap<_, _> =
                                            Default::default();

                                        for (hostname, cert_uri) in ca_certs {
                                            let cert_id = format!("content-trust-{}", hostname);
                                            certd_config.preloaded_certs.insert(
                                                cert_id.clone(),
                                                aziot_certd_config::PreloadedCert::Uri(cert_uri),
                                            );
                                            new_ca_certs.insert(hostname.clone(), cert_id);
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

    let header = String::from(
        "\
        # This file is auto-generated by `iotedge config apply`\n\
        # Do not edit it manually; any edits will be lost when the command is run again.\n\
        \n\
    ",
    );

    let mut keyd_config_out = header.clone();
    keyd_config_out.push_str(
        &toml::to_string(&keyd_config)
            .map_err(|err| format!("could not serialize aziot-keyd config: {}", err))?,
    );

    let mut certd_config_out = header.clone();
    certd_config_out.push_str(
        &toml::to_string(&certd_config)
            .map_err(|err| format!("could not serialize aziot-certd config: {}", err))?,
    );

    let mut identityd_config_out = header.clone();
    identityd_config_out.push_str(
        &toml::to_string(&identityd_config)
            .map_err(|err| format!("could not serialize aziot-identityd config: {}", err))?,
    );

    let mut tpmd_config_out = header.clone();
    tpmd_config_out.push_str(
        &toml::to_string(&tpmd_config)
            .map_err(|err| format!("could not serialize aziot-tpmd config: {}", err))?,
    );

    let mut edged_config_out = header;
    edged_config_out.push_str(
        &toml::to_string(&edged_config)
            .map_err(|err| format!("could not serialize aziot-edged config: {}", err))?,
    );

    Ok(RunOutput {
        certd_config: certd_config_out,
        identityd_config: identityd_config_out,
        keyd_config: keyd_config_out,
        tpmd_config: tpmd_config_out,
        edged_config: edged_config_out,
        preloaded_device_id_pk_bytes,
        preloaded_master_encryption_key_bytes,
    })
}

fn set_quickstart_ca(
    keyd_config: &mut aziot_keyd_config::Config,
    certd_config: &mut aziot_certd_config::Config,
    aziotcs_uid: nix::unistd::Uid,
    expiry_days: Option<u32>,
    subject: Option<aziot_certd_config::CertSubject>,
) {
    let issuance = aziot_certd_config::CertIssuanceOptions {
        method: aziot_certd_config::CertIssuanceMethod::SelfSigned,
        expiry_days,
        subject,
    };

    certd_config.cert_issuance.certs.insert(
        edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned(),
        issuance.clone(),
    );

    let mut certd_keys = vec![edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_owned()];

    let temp_cert = format!("{}-temp", edgelet_settings::AZIOT_EDGED_CA_ALIAS);

    certd_config
        .cert_issuance
        .certs
        .insert(temp_cert.clone(), issuance);
    certd_keys.push(temp_cert);

    keyd_config.principal.push(aziot_keyd_config::Principal {
        uid: aziotcs_uid.as_raw(),
        keys: certd_keys,
    });
}

#[cfg(test)]
mod tests {
    #[tokio::test]
    async fn test() {
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

            let super_config_file = case_directory.join("super-config.toml");
            let expected_keyd_config = std::fs::read(case_directory.join("keyd.toml")).unwrap();
            let expected_keyd_config = std::str::from_utf8(&expected_keyd_config).unwrap();
            let expected_certd_config = std::fs::read(case_directory.join("certd.toml")).unwrap();
            let expected_certd_config = std::str::from_utf8(&expected_certd_config).unwrap();
            let expected_identityd_config =
                std::fs::read(case_directory.join("identityd.toml")).unwrap();
            let expected_identityd_config =
                std::str::from_utf8(&expected_identityd_config).unwrap();
            let expected_tpmd_config = std::fs::read(case_directory.join("tpmd.toml")).unwrap();
            let expected_tpmd_config = std::str::from_utf8(&expected_tpmd_config).unwrap();
            let expected_edged_config = std::fs::read(case_directory.join("edged.toml")).unwrap();
            let expected_edged_config = std::str::from_utf8(&expected_edged_config).unwrap();

            let expected_preloaded_device_id_pk_bytes =
                match std::fs::read(case_directory.join("device-id")) {
                    Ok(contents) => Some(contents),
                    Err(err) if err.kind() == std::io::ErrorKind::NotFound => None,
                    Err(err) => panic!("could not read device-id file: {}", err),
                };

            let expected_preloaded_master_encryption_key_bytes = {
                match std::fs::read(case_directory.join("master-encryption-key")) {
                    Ok(contents) => {
                        match std::fs::write("/tmp/master-encryption-key", &contents) {
                            Ok(()) => (),
                            Err(err) if err.kind() == std::io::ErrorKind::NotFound => (),
                            Err(err) => {
                                panic!("could not create temp master-encryption-key file: {}", err);
                            }
                        }
                        Some(contents)
                    }
                    Err(err) if err.kind() == std::io::ErrorKind::NotFound => {
                        match std::fs::remove_file("/tmp/master-encryption-key") {
                            Ok(()) => (),
                            Err(err) if err.kind() == std::io::ErrorKind::NotFound => (),
                            Err(err) => {
                                panic!("could not delete temp master-encryption-key file: {}", err);
                            }
                        }
                        None
                    }
                    Err(err) => panic!("could not read master-encryption-key file: {}", err),
                }
            };

            let super::RunOutput {
                keyd_config: actual_keyd_config,
                certd_config: actual_certd_config,
                identityd_config: actual_identityd_config,
                tpmd_config: actual_tpmd_config,
                edged_config: actual_edged_config,
                preloaded_device_id_pk_bytes: actual_preloaded_device_id_pk_bytes,
                preloaded_master_encryption_key_bytes: actual_preloaded_master_encryption_key_bytes,
            } = super::execute_inner(
                &super_config_file,
                nix::unistd::Uid::from_raw(5555),
                nix::unistd::Uid::from_raw(5556),
                nix::unistd::Uid::from_raw(5558),
            )
            .await
            .unwrap();

            // Convert the file contents to bytes::Bytes before asserting, because bytes::Bytes's Debug format
            // prints human-readable strings instead of raw u8s.
            assert_eq!(
                toml::from_str::<toml::Value>(expected_keyd_config).expect("Valid toml"),
                toml::from_str::<toml::Value>(&actual_keyd_config).expect("Valid toml"),
                "keyd config does not match"
            );
            assert_eq!(
                toml::from_str::<toml::Value>(expected_certd_config).expect("Valid toml"),
                toml::from_str::<toml::Value>(&actual_certd_config).expect("Valid toml"),
                "certd config does not match"
            );
            assert_eq!(
                toml::from_str::<toml::Value>(expected_identityd_config).expect("Valid toml"),
                toml::from_str::<toml::Value>(&actual_identityd_config).expect("Valid toml"),
                "identityd config does not match"
            );
            assert_eq!(
                toml::from_str::<toml::Value>(expected_tpmd_config).expect("Valid toml"),
                toml::from_str::<toml::Value>(&actual_tpmd_config).expect("Valid toml"),
                "tpmd config does not match"
            );
            assert_eq!(
                toml::from_str::<toml::Value>(expected_edged_config).expect("Valid toml"),
                toml::from_str::<toml::Value>(&actual_edged_config).expect("Valid toml"),
                "edged config does not match"
            );
            assert_eq!(
                expected_preloaded_device_id_pk_bytes, actual_preloaded_device_id_pk_bytes,
                "device ID key bytes do not match"
            );
            assert_eq!(
                expected_preloaded_master_encryption_key_bytes,
                actual_preloaded_master_encryption_key_bytes,
                "imported master encryption key bytes do not match"
            );
        }
    }
}
