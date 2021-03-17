// Copyright (c) Microsoft. All rights reserved.

//! This subcommand takes the super-config file, converts it into the individual services' config files,
//! writes those files, and restarts the services.

use std::path::Path;

use aziotctl_common::config as common_config;

use super::super_config;

const AZIOT_EDGED_HOMEDIR_PATH: &str = "/var/lib/aziot/edged";

const TRUST_BUNDLE_USER_ALIAS: &str = "trust-bundle-user";

// TODO: Dedupe this with edgelet-http-workload
const IOTEDGED_COMMONNAME: &str = "iotedged workload ca";

pub fn execute(config: &Path) -> Result<(), std::borrow::Cow<'static, str>> {
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
                .ok_or_else(|| "could not query aziotks user information")?;

            let aziotcs_user = nix::unistd::User::from_name("aziotcs")
                .map_err(|err| format!("could not query aziotcs user information: {}", err))?
                .ok_or_else(|| "could not query aziotcs user information")?;

            let aziotid_user = nix::unistd::User::from_name("aziotid")
                .map_err(|err| format!("could not query aziotid user information: {}", err))?
                .ok_or_else(|| "could not query aziotid user information")?;

            let aziottpm_user = nix::unistd::User::from_name("aziottpm")
                .map_err(|err| format!("could not query aziottpm user information: {}", err))?
                .ok_or_else(|| "could not query aziottpm user information")?;

            let iotedge_user = nix::unistd::User::from_name("iotedge")
                .map_err(|err| format!("could not query iotedge user information: {}", err))?
                .ok_or_else(|| "could not query iotedge user information")?;

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
                .ok_or_else(|| ("could not query current user information"))?;
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
    } = execute_inner(config, aziotcs_user.uid, aziotid_user.uid, iotedge_user.uid)?;

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

    common_config::write_file(
        "/etc/aziot/keyd/config.d/00-super.toml",
        &keyd_config,
        &aziotks_user,
        0o0600,
    )
    .map_err(|err| format!("{:?}", err))?;

    common_config::write_file(
        "/etc/aziot/certd/config.d/00-super.toml",
        &certd_config,
        &aziotcs_user,
        0o0600,
    )
    .map_err(|err| format!("{:?}", err))?;

    common_config::write_file(
        "/etc/aziot/identityd/config.d/00-super.toml",
        &identityd_config,
        &aziotid_user,
        0o0600,
    )
    .map_err(|err| format!("{:?}", err))?;

    common_config::write_file(
        "/etc/aziot/tpmd/config.d/00-super.toml",
        &tpmd_config,
        &aziottpm_user,
        0o0600,
    )
    .map_err(|err| format!("{:?}", err))?;

    common_config::write_file(
        "/etc/aziot/edged/config.d/00-super.toml",
        &edged_config,
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
    certd_config: Vec<u8>,
    identityd_config: Vec<u8>,
    keyd_config: Vec<u8>,
    tpmd_config: Vec<u8>,
    edged_config: Vec<u8>,
    preloaded_device_id_pk_bytes: Option<Vec<u8>>,
}

fn execute_inner(
    config: &std::path::Path,
    aziotcs_uid: nix::unistd::Uid,
    aziotid_uid: nix::unistd::Uid,
    iotedge_uid: nix::unistd::Uid,
) -> Result<RunOutput, std::borrow::Cow<'static, str>> {
    let config = std::fs::read(config)
        .map_err(|err| format!("could not read config file {}: {}", config.display(), err))?;

    let super_config::Config {
        parent_hostname,
        trust_bundle_cert,
        aziot,
        agent,
        connect,
        listen,
        watchdog,
        edge_ca,
        moby_runtime,
    } = toml::from_slice(&config).map_err(|err| format!("could not parse config file: {}", err))?;

    let aziotctl_common::config::apply::RunOutput {
        mut certd_config,
        mut identityd_config,
        mut keyd_config,
        tpmd_config,
        preloaded_device_id_pk_bytes,
    } = aziotctl_common::config::apply::run(aziot, aziotcs_uid, aziotid_uid)
        .map_err(|err| format!("{:?}", err))?;

    certd_config.principal.push(aziot_certd_config::Principal {
        uid: iotedge_uid.as_raw(),
        certs: vec![
            edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned(),
            "aziot-edged/module/*".to_owned(),
        ],
    });

    identityd_config
        .principal
        .push(aziot_identityd_config::Principal {
            uid: aziot_identityd_config::Uid(iotedge_uid.as_raw()),
            name: aziot_identity_common::ModuleId("aziot-edge".to_owned()),
            id_type: None,
            localid: None,
        });

    keyd_config.principal.push(aziot_keyd_config::Principal {
        uid: iotedge_uid.as_raw(),
        keys: vec![
            edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned(),
            "iotedge_master_encryption_id".to_owned(),
        ],
    });

    let mut trust_bundle_certs = vec![edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned()];

    let edge_ca = edge_ca.unwrap_or(super_config::EdgeCa::Quickstart {
        auto_generated_edge_ca_expiry_days: 1,
    });

    let (edge_ca_cert, edge_ca_key) = match edge_ca {
        super_config::EdgeCa::Explicit { cert, pk } => {
            keyd_config.preloaded_keys.insert(
                edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned(),
                pk.to_string(),
            );

            certd_config.preloaded_certs.insert(
                edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned(),
                aziot_certd_config::PreloadedCert::Uri(cert),
            );

            (
                Some(edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned()),
                Some(edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned()),
            )
        }
        super_config::EdgeCa::Quickstart {
            auto_generated_edge_ca_expiry_days,
        } => {
            certd_config.cert_issuance.certs.insert(
                edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned(),
                aziot_certd_config::CertIssuanceOptions {
                    method: aziot_certd_config::CertIssuanceMethod::SelfSigned,
                    common_name: Some(IOTEDGED_COMMONNAME.to_owned()),
                    expiry_days: Some(auto_generated_edge_ca_expiry_days),
                },
            );

            keyd_config.principal.push(aziot_keyd_config::Principal {
                uid: aziotcs_uid.as_raw(),
                keys: vec![edgelet_core::AZIOT_EDGED_CA_ALIAS.to_owned()],
            });

            (None, None)
        }
    };

    if let Some(trust_bundle_cert) = trust_bundle_cert {
        certd_config.preloaded_certs.insert(
            TRUST_BUNDLE_USER_ALIAS.to_owned(),
            aziot_certd_config::PreloadedCert::Uri(trust_bundle_cert),
        );

        trust_bundle_certs.push(TRUST_BUNDLE_USER_ALIAS.to_owned());
    }

    certd_config.preloaded_certs.insert(
        edgelet_core::TRUST_BUNDLE_ALIAS.to_owned(),
        aziot_certd_config::PreloadedCert::Ids(trust_bundle_certs),
    );

    // TODO: Remove this when IS gains first-class support for parent_hostname
    if let Some(parent_hostname) = &parent_hostname {
        if let aziot_identityd_config::ProvisioningType::Manual {
            iothub_hostname, ..
        } = &mut identityd_config.provisioning.provisioning
        {
            *iothub_hostname = parent_hostname.clone();
        }
    }

    let edged_config = edgelet_docker::Settings {
        base: edgelet_core::Settings {
            hostname: identityd_config.hostname.clone(),
            parent_hostname,

            edge_ca_cert,
            edge_ca_key,
            trust_bundle_cert: Some(edgelet_core::TRUST_BUNDLE_ALIAS.to_owned()),

            homedir: AZIOT_EDGED_HOMEDIR_PATH.into(),

            agent,

            connect,
            listen,

            watchdog,

            endpoints: Default::default(),
        },

        moby_runtime: {
            let super_config::MobyRuntime {
                uri,
                network,
                content_trust,
            } = moby_runtime;

            edgelet_docker::MobyRuntime {
                uri,
                network,
                content_trust: content_trust
                    .map(
                        |content_trust| -> Result<_, std::borrow::Cow<'static, str>> {
                            let super_config::ContentTrust { ca_certs } = content_trust;

                            Ok(edgelet_docker::ContentTrust {
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
                                            new_ca_certs.insert(hostname.to_owned(), cert_id);
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

    let header = b"\
        # This file is auto-generated by `iotedge config apply`\n\
        # Do not edit it manually; any edits will be lost when the command is run again.\n\
        \n\
    ";

    let keyd_config: Vec<_> = header
        .iter()
        .copied()
        .chain(
            toml::to_vec(&keyd_config)
                .map_err(|err| format!("could not serialize aziot-keyd config: {}", err))?,
        )
        .collect();
    let certd_config: Vec<_> = header
        .iter()
        .copied()
        .chain(
            toml::to_vec(&certd_config)
                .map_err(|err| format!("could not serialize aziot-certd config: {}", err))?,
        )
        .collect();
    let identityd_config: Vec<_> = header
        .iter()
        .copied()
        .chain(
            toml::to_vec(&identityd_config)
                .map_err(|err| format!("could not serialize aziot-identityd config: {}", err))?,
        )
        .collect();
    let tpmd_config: Vec<_> = header
        .iter()
        .copied()
        .chain(
            toml::to_vec(&tpmd_config)
                .map_err(|err| format!("could not serialize aziot-tpmd config: {}", err))?,
        )
        .collect();
    let edged_config: Vec<_> = header
        .iter()
        .copied()
        .chain(
            toml::to_vec(&edged_config)
                .map_err(|err| format!("could not serialize aziot-edged config: {}", err))?,
        )
        .collect();

    Ok(RunOutput {
        certd_config,
        identityd_config,
        keyd_config,
        tpmd_config,
        edged_config,
        preloaded_device_id_pk_bytes,
    })
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

            let super_config_file = case_directory.join("super-config.toml");
            let expected_keyd_config = std::fs::read(case_directory.join("keyd.toml")).unwrap();
            let expected_certd_config = std::fs::read(case_directory.join("certd.toml")).unwrap();
            let expected_identityd_config =
                std::fs::read(case_directory.join("identityd.toml")).unwrap();
            let expected_tpmd_config = std::fs::read(case_directory.join("tpmd.toml")).unwrap();
            let expected_edged_config = std::fs::read(case_directory.join("edged.toml")).unwrap();

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
                preloaded_device_id_pk_bytes: actual_preloaded_device_id_pk_bytes,
            } = super::execute_inner(
                &super_config_file,
                nix::unistd::Uid::from_raw(5555),
                nix::unistd::Uid::from_raw(5556),
                nix::unistd::Uid::from_raw(5558),
            )
            .unwrap();

            // Convert the file contents to bytes::Bytes before asserting, because bytes::Bytes's Debug format
            // prints human-readable strings instead of raw u8s.
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
                expected_preloaded_device_id_pk_bytes.map(bytes::Bytes::from),
                actual_preloaded_device_id_pk_bytes.map(bytes::Bytes::from),
                "device ID key bytes do not match"
            );
        }
    }
}
