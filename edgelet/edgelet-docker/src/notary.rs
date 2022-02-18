// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::fs;
use std::fs::File;
use std::path::{Path, PathBuf};
use std::process::Command;

use failure::ResultExt;
use futures::Future;
use log::debug;
use serde_json::json;

use crate::{Error, ErrorKind};

pub fn notary_init(
    home_dir: &Path,
    registry_server_hostname: &str,
    cert_buf: &[u8],
) -> Result<PathBuf, Error> {
    // Validate inputs
    if registry_server_hostname.is_empty() {
        return Err(ErrorKind::InitializeNotary("hostname is empty".to_owned()).into());
    }

    if cert_buf.is_empty() {
        return Err(
            ErrorKind::InitializeNotary("root ca pem string content is empty".to_owned()).into(),
        );
    }

    // Directory structure example
    // home directory : /var/lib/aziot/edged
    // notary directory : /var/lib/aziot/edged/notary
    // hostname directory : /var/lib/aziot/edged/notary/sanitized_hostname
    // trust collection directory : /var/lib/aziot/edged/notary/sanitized_hostname/trust_collection
    // certs directory for each hostname : /var/lib/aziot/edged/notary/sanitized_hostname/certs
    // notary config file path : /var/lib/aziot/edged/notary/sanitized_hostname/config

    // Create notary directory under home directory
    let notary_dir = home_dir.join("notary");

    // Create a folder name with santized hostname
    let mut hostname_dir = notary_dir.clone();
    let mut sanitized_hostname = String::new();
    for c in registry_server_hostname.chars() {
        if c.is_ascii_alphanumeric() || !c.is_ascii() {
            sanitized_hostname.push(c);
        } else {
            sanitized_hostname.push_str(&format!("%{:02x}", c as u8));
        }
    }
    hostname_dir.push(&sanitized_hostname);

    // Build trust collection and certs directory for each hostname
    let trust_dir = hostname_dir.join("trust_collection");
    debug!(
        "Trust directory for {} is {}",
        sanitized_hostname,
        trust_dir.display()
    );
    let certs_dir = hostname_dir.join("certs");
    debug!(
        "Certs directory for {} is {}",
        sanitized_hostname,
        certs_dir.display()
    );

    // Delete Notary directory for a clean start.
    if let Err(err) = fs::remove_dir_all(&notary_dir) {
        if err.kind() != std::io::ErrorKind::NotFound {
            return Err(ErrorKind::InitializeNotary(format!(
                "could not delete notary directory {}",
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
    let root_ca_cert_name = sanitized_hostname + "_root_ca.pem";
    let root_ca_file_path = certs_dir.join(root_ca_cert_name);

    fs::write(&root_ca_file_path, cert_buf).with_context(|_| {
        ErrorKind::InitializeNotary(format!(
            "could not create root CA cert for notary hostname directory {}",
            hostname_dir.display()
        ))
    })?;

    // Add https to hostname
    let input_url_https = format!("https://{}", registry_server_hostname);
    debug!("URL with https is {}", input_url_https);

    // Create Notary Config.json contents
    let config_contents = json!({
      "trust_dir": trust_dir,
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

    // Create config directory
    fs::create_dir_all(&config_file_path).with_context(|_| {
        ErrorKind::InitializeNotary(format!(
            "could not config directory {}",
            config_file_path.display()
        ))
    })?;

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
            "could not write contents to notary config file in {}",
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

    notary_cmd
        .args(&["lookup", image_gun, tag, "-c"])
        .arg(config_path);

    if let Some(notary_auth) = notary_auth {
        notary_cmd.env("NOTARY_AUTH", notary_auth);
    }

    let (send, recv) = tokio::sync::oneshot::channel();

    std::thread::spawn(move || {
        send.send(
            notary_cmd
                .output()
                .with_context(|e| {
                    ErrorKind::LaunchNotary(format!("could not spawn notary process: {}", e))
                })
                .map_err(Error::from)
                .and_then(|std::process::Output { stdout, .. }| {
                    let output_str = std::str::from_utf8(&stdout).with_context(|_| {
                        ErrorKind::LaunchNotary("received invalid utf8".to_owned())
                    })?;
                    debug!("Notary output string is {}", output_str);

                    output_str
                        .split_whitespace()
                        .nth(2)
                        .map(ToOwned::to_owned)
                        .ok_or_else(|| {
                            ErrorKind::LaunchNotary("notary digest split array is empty".to_owned())
                                .into()
                        })
                }),
        )
        .unwrap()
    });

    recv.map_err(|e| ErrorKind::LaunchNotary(format!("failed to receive notary output: {}", e)))
        .flatten()
        .map(|output| (output, lock))
}
