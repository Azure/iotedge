use crate::{Error, ErrorKind};
use failure::ResultExt;
use log::{debug, info};
use serde_json::json;
use std::fs;
use std::fs::File;
use std::path::{Path, PathBuf};

pub fn notary_init(homedir: &Path, hostname: &str, path: &PathBuf) -> Result<PathBuf, Error> {
    // Validate inputs
    if hostname.is_empty() {
        return Err(ErrorKind::InitializeNotary("hostname is empty".to_owned()).into());
    }

    if !path.exists() {
        return Err(ErrorKind::InitializeNotary(format!(
            "root ca at path {} does not exist",
            path.display()
        ))
        .into());
    }

    // Create notary home directory
    let notary_dir = homedir.join("notary");

    // Create a folder name with santized hostname
    let mut trust_dir = notary_dir;
    let mut filename = String::new();
    for c in hostname.chars() {
        if c.is_ascii_alphanumeric() || !c.is_ascii() {
            filename.push(c);
        } else {
            filename.push_str(&format!("%{:02x}", c as u8));
        }
    }
    trust_dir.push(filename);

    // Create trust directory
    info!("Trust directory is {}", trust_dir.display());
    if let Err(err) = fs::remove_dir_all(&trust_dir) {
        if err.kind() != std::io::ErrorKind::NotFound {
            return Err(ErrorKind::InitializeNotary(format!(
                "could not delete trust directory {}",
                trust_dir.display()
            ))
            .into());
        }
    }
    fs::create_dir_all(&trust_dir).with_context(|_| {
        ErrorKind::InitializeNotary(format!(
            "could not create trust directory {}",
            trust_dir.display()
        ))
    })?;

    // Add https to hostname
    let input_url_https = format!("https://{}", hostname);
    debug!("URL with https is {}", input_url_https);

    // Create Notary Config.json contents
    let config_contents = json!({
      "trust_dir": trust_dir,
      "remote_server": {
        "url" : input_url_https,
      },
      "trust_pinning": {
        "ca": {
          "": path,
        },
        "disable_tofu": "true"
      }
    });
    debug!("Config JSON contents {}", config_contents);

    // Generate Notary Config.json path
    let mut config_file_path = trust_dir;
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
    // config_file_path;
}

#[cfg(test)]
mod test {
    use crate::notary;
    use crate::ErrorKind;
    use std::path::PathBuf;
    use tempfile::NamedTempFile;

    #[test]
    fn check_for_empty_hostname() {
        let hostname = String::new();
        let home_dir = NamedTempFile::new().unwrap();
        let root_ca_file = NamedTempFile::new().unwrap();
        let result = notary::notary_init(
            home_dir.path(),
            &hostname,
            &root_ca_file.path().to_path_buf(),
        );
        let err = result.unwrap_err();
        assert!(matches!(err.kind(), ErrorKind::InitializeNotary(s) if s == "hostname is empty"));
    }

    #[test]
    fn check_for_root_ca_file_does_not_exist() {
        let hostname = r"myregistry.azurecr.io";
        let home_dir = NamedTempFile::new().unwrap();
        let root_ca_file = PathBuf::from("filedoesnotexist.crt");
        let result = notary::notary_init(home_dir.path(), &hostname, &root_ca_file);
        let err = result.unwrap_err();
        let _display_msg = format!("root ca at path {} does not exist", root_ca_file.display());
        assert!(matches!(
            err.kind(),
            ErrorKind::InitializeNotary(_display_msg)
        ));
    }
}
