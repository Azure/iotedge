// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::fs;
use std::fs::File;
use std::path::{Path, PathBuf};
use std::process::Command;

use failure::ResultExt;
use futures::Future;
use log::{debug, info};
use serde_json::json;
use tokio_process::CommandExt;

use crate::{Error, ErrorKind};

pub fn notary_init(
    home_dir: &Path,
    registry_server_hostname: &str,
    cert_buf: &Vec<u8>,
) -> Result<PathBuf, Error> {
    // Validate inputs
    if registry_server_hostname.is_empty() {
        return Err(ErrorKind::InitializeNotary("hostname is empty".to_owned()).into());
    }

    if cert_buf.is_empty() {
        return Err(ErrorKind::InitializeNotary("root ca pem string content is empty".to_owned()).into());
    }

    // Directory structure example 
    // home directory : /home 
    // notary directory : /home/notary
    // hostname directory : /home/notary/sanitized_hostname
    // trust collection directory : /home/notary/santized_hostname/trust_collection
    // certs directory for each hostname : /home/notary/santized_hostname/certs
    // notary config file path : /home/notary/santized_hostname/config

    // Create notary directory under home directory
    let notary_dir = home_dir.join("notary");

    // Create a folder name with santized hostname
    let mut hostname_dir = notary_dir;
    let mut file_name = String::new();
    for c in registry_server_hostname.chars() {
        if c.is_ascii_alphanumeric() || !c.is_ascii() {
            file_name.push(c);
        } else {
            file_name.push_str(&format!("%{:02x}", c as u8));
        }
    }
    hostname_dir.push(&file_name);

    // Build trust collection and certs directory for each hostname
    let trust_dir = hostname_dir.join("trust_collection");
    info!("Trust directory for {} is {}", file_name, trust_dir.display());
    let certs_dir = hostname_dir.join("certs");
    info!("Certs directory for {} is {}", file_name, trust_dir.display());

    // Delete hostname directory for a clean start. 
    if let Err(err) = fs::remove_dir_all(&hostname_dir) {
        if err.kind() != std::io::ErrorKind::NotFound {
            return Err(ErrorKind::InitializeNotary(format!(
                "could not delete notary hostname directory {}",
                hostname_dir.display()
            ))
            .into());
        }
    }

    // Create trust directory
    fs::create_dir_all(&trust_dir).with_context(|_| {
        ErrorKind::InitializeNotary(format!(
            "could not create trust directory {}",
            trust_dir.display()
        ))
    })?;

    // Create certs directory
    fs::create_dir_all(&certs_dir).with_context(|_| {
        ErrorKind::InitializeNotary(format!(
            "could not create certs directory {}",
            certs_dir.display()
        ))
    })?;

    // Create root CA file name 
    let root_ca_cert_name = file_name + "_root_ca.pem";

    fs::write(&root_ca_cert_name, cert_buf).with_context(|_| {
        ErrorKind::InitializeNotary(format!(
            "could not create root CA cert for notary hostname directory {}",
            hostname_dir.display()
        ))
    })?;
    let root_ca_file_path = certs_dir.join(root_ca_cert_name);

    // Add https to hostname
    let input_url_https = format!("https://{}", registry_server_hostname);
    debug!("URL with https is {}", input_url_https);

    // Create Notary Config.json contents
    let config_contents = json!({
      "trust_dir": hostname_dir,
      "remote_server": {
        "url" : input_url_https,
      },
      "trust_pinning": {
        "ca": {
          "": root_ca_file_path,
        },
        "disable_tofu": "true"
      }
    });
    debug!("Config JSON contents {}", config_contents);

    // Generate Notary Config.json path
    let mut config_file_path = hostname_dir.join("config");
    config_file_path.push(r"config.json");
    debug!("Config file path {}", config_file_path.display());

    // Create Notary config file
    let file = File::create(&config_file_path).with_context(|_| {
        ErrorKind::InitializeNotary(format!(
            "could not create notary config file in {}",
            config_file_path.display()
        ))
    })?;
    serde_json::to_writer(file, &config_contents).with_context(|_| {
        ErrorKind::InitializeNotary(format!(
            "could not create notary config file in {}",
            config_file_path.display()
        ))
    })?;
    debug!("Config JSON file created successfully");
    Ok(config_file_path)
}

pub fn notary_lookup(
    notary_auth: Option<&str>,
    image_gun: &str,
    tag: &str,
    config_path: &Path,
    lock: tokio::sync::lock::LockGuard<BTreeMap<String, String>>,
) -> impl Future<
    Item = (
        String,
        tokio::sync::lock::LockGuard<BTreeMap<String, String>>,
    ),
    Error = Error,
> {
    let mut notary_cmd = Command::new("notary");
    if let Some(notary_auth) = notary_auth {
        notary_cmd.env("NOTARY_AUTH", notary_auth);
    }
    notary_cmd
        .arg("lookup")
        .args(&[image_gun, tag])
        .arg("-c")
        .arg(config_path)
        .output_async()
        .then(|notary_output| {
            let notary_output = notary_output.with_context(|_| {
                ErrorKind::LaunchNotary("could not spawn notary process".to_owned())
            })?;
            let notary_output_string =
                String::from_utf8(notary_output.stdout).with_context(|_| {
                    ErrorKind::LaunchNotary("could not retrieve notary output as string".to_owned())
                })?;
            debug!("Notary output string is {}", notary_output_string);
            let split_array: Vec<&str> = notary_output_string.split_whitespace().collect();
            if split_array.len() < 2 {
                return Err(ErrorKind::LaunchNotary(
                    "notary digest split array is empty".to_owned(),
                )
                .into());
            }

            // Notary Server output on lookup is of the format [tag, digest, bytes]
            Ok((split_array[1].to_string(), lock))
        })
}

// #[cfg(test)]
// mod test {
//     use std::path::PathBuf;

//     use tempfile::NamedTempFile;

//     use crate::notary;
//     use crate::ErrorKind;

//     #[test]
//     fn check_for_empty_hostname() {
//         let registry_server_hostname = String::new();
//         let home_dir = NamedTempFile::new().unwrap();
//         let root_ca_file = NamedTempFile::new().unwrap();
//         let result = notary::notary_init(
//             home_dir.path(),
//             &registry_server_hostname,
//             &root_ca_file.path().to_path_buf(),
//         );
//         let err = result.unwrap_err();
//         assert!(matches!(err.kind(), ErrorKind::InitializeNotary(s) if s == "hostname is empty"));
//     }

//     #[test]
//     fn check_for_root_ca_file_does_not_exist() {
//         let registry_server_hostname = r"myregistry.azurecr.io";
//         let home_dir = NamedTempFile::new().unwrap();
//         let root_ca_file = PathBuf::from("filedoesnotexist.crt");
//         let result = notary::notary_init(home_dir.path(), &registry_server_hostname, &root_ca_file);
//         let err = result.unwrap_err();
//         let display_msg = format!("root ca at path {} does not exist", root_ca_file.display());
//         assert!(matches!(
//             err.kind(),
//             ErrorKind::InitializeNotary(s) if s == &display_msg
//         ));
//     }
// }
