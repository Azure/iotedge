use std::path::{Path, PathBuf};

use serde::de::DeserializeOwned;
use serde_json::json;
use tokio::fs::{File, OpenOptions};
use tokio::io::{AsyncReadExt, AsyncWriteExt};

use azure_iot_mqtt::{TwinProperties, TwinState};

use super::deployment::Deployment;

type Result<T> = std::result::Result<T, Box<dyn std::error::Error>>;

pub struct DeploymentManager {
    current_location: PathBuf,
    valid_location: PathBuf,
    current_deployment: serde_json::Value,
    valid_deployment: Option<Deployment>,
}

impl DeploymentManager {
    pub async fn new(storage_location: impl AsRef<Path>) -> Result<Self> {
        let current_location = storage_location.as_ref().join("newest_deployment.json");
        let current_deployment = read_serde(&current_location).await?;

        let valid_location = storage_location.as_ref().join("valid_deployment.json");
        let valid_deployment: Option<Deployment> = read_serde(&valid_location).await?;
        let valid_deployment: Option<Deployment> =
            valid_deployment.or_else(|| match Self::validate_deployment(&current_deployment) {
                Ok(d) => d,
                Err(e) => {
                    println!("Can not parse curret deployment: {}", e);
                    None
                }
            });

        Ok(Self {
            current_location,
            valid_location,
            current_deployment,
            valid_deployment,
        })
    }

    pub async fn set_deployment(&mut self, deployment: TwinState) -> Result<()> {
        self.current_deployment = json!({ "properties": deployment });
        write_serde(&self.current_location, &self.current_deployment).await?;

        if let Some(deployment) = Self::validate_deployment(&self.current_deployment)? {
            self.valid_deployment = Some(deployment);
            write_serde(&self.valid_location, &self.valid_deployment).await?;
        }

        Ok(())
    }

    pub async fn update_deployment(&mut self, patch: TwinProperties) -> Result<()> {
        let patch = json!({ "properties": { "desired": patch }});
        json_patch::merge(&mut self.current_deployment, &patch);
        write_serde(&self.current_location, &self.current_deployment).await?;

        if let Some(deployment) = Self::validate_deployment(&self.current_deployment)? {
            self.valid_deployment = Some(deployment);
            write_serde(&self.valid_location, &self.valid_deployment).await?;
        }

        Ok(())
    }

    pub fn get_valid_deployment(&self) -> Option<&Deployment> {
        self.valid_deployment.as_ref()
    }

    fn validate_deployment(deployment: &serde_json::Value) -> Result<Option<Deployment>> {
        let deployment = serde_json::from_value(deployment.clone())?;

        Ok(Some(deployment))
        // Ok(Default::default())
    }
}

async fn write_serde(file: impl AsRef<Path>, value: impl serde::Serialize) -> Result<()> {
    let value = serde_json::to_string(&value)?;
    File::create(file)
        .await?
        .write_all(value.as_bytes())
        .await?;

    Ok(())
}

async fn read_serde<T>(file: impl AsRef<Path>) -> Result<T>
where
    T: DeserializeOwned + Default,
{
    let file = OpenOptions::new().read(true).open(file).await;

    match file {
        Ok(mut file) => {
            let mut contents = vec![];
            file.read_to_end(&mut contents).await.unwrap();

            let result = serde_json::from_slice(&contents)?;
            Ok(result)
        }
        Err(err) if err.kind() == std::io::ErrorKind::NotFound => Ok(Default::default()),
        Err(err) => Err(err.into()),
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use super::*;
    use rand::Rng;
    use serde_json::json;
    use tempfile::tempdir;
    use tokio::select;

    #[tokio::test]
    async fn empty_directory() {
        let tmp_dir = tempdir().unwrap();
        let tmp_dir = tmp_dir.path();

        let manager = DeploymentManager::new(tmp_dir)
            .await
            .expect("Create Deployment Manager");

        let empty: serde_json::Value = Default::default();
        assert_eq!(manager.current_deployment, empty);
        assert_eq!(manager.valid_deployment, None);
    }

    #[tokio::test]
    async fn load_current() {
        let test_file = std::path::Path::new(concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/src/deployment/test/twin1.json"
        ));
        let tmp_dir = tempdir().unwrap();
        let tmp_dir = tmp_dir.path();

        tokio::fs::copy(test_file, tmp_dir.join("newest_deployment.json"))
            .await
            .expect("Copy Test File");

        let manager = DeploymentManager::new(tmp_dir)
            .await
            .expect("Create Deployment Manager");

        let expected: serde_json::Value =
            read_serde(test_file).await.expect("Test file is parsable");
        assert_eq!(manager.current_deployment, expected);
    }

    #[tokio::test]
    async fn update_deployment() {
        let tmp_dir = tempdir().unwrap();
        let tmp_dir = tmp_dir.path();

        let mut manager = DeploymentManager::new(tmp_dir)
            .await
            .expect("Create Deployment Manager");

        let deployment = TwinState {
            desired: TwinProperties {
                version: 1,
                properties: [(
                    "modules".to_owned(),
                    json!({
                        "simulated_temp": {
                            "settings": {
                                "image": "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor",
                                "createOptions": ""
                            },
                            "type": "docker",
                            "status": "running",
                            "restartPolicy": "always",
                            "version" : "1.0"
                        }
                    }),
                )]
                .iter()
                .cloned()
                .collect(),
            },
            ..Default::default()
        };
        manager
            .set_deployment(deployment)
            .await
            .expect("set deployment");

        let patch = TwinProperties {
            version: 2,
            properties: [(
                "modules".to_owned(),
                json!({
                    "simulated_temp": {
                        "restartPolicy": "never"
                    }
                }),
            )]
            .iter()
            .cloned()
            .collect(),
        };
        manager
            .update_deployment(patch)
            .await
            .expect("Able to update deployment");

        let expected = json!({
            "$version": 2,
            "modules": {
                "simulated_temp": {
                    "settings": {
                        "image": "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor",
                        "createOptions": ""
                    },
                    "type": "docker",
                    "status": "running",
                    "restartPolicy": "never",
                    "version" : "1.0"
                }
            }
        });
        let actual = manager
            .current_deployment
            .get("properties")
            .expect("properties field exists")
            .get("desired")
            .expect("desired field exists")
            .to_owned();

        assert_eq!(expected, actual, "restart policy is changed"); // TODO: check restart policy on parsed type
    }

    #[tokio::test]
    async fn test_parsing() {
        let test_files_directory =
            std::path::Path::new(concat!(env!("CARGO_MANIFEST_DIR"), "/src/deployment/test"));
        let tmp_dir = tempdir().unwrap();
        let tmp_dir = tmp_dir.path();

        for test_file in std::fs::read_dir(test_files_directory).unwrap() {
            let test_file = test_file.unwrap();
            if test_file.file_type().unwrap().is_dir() {
                continue;
            }
            tokio::fs::copy(test_file.path(), tmp_dir.join("newest_deployment.json"))
                .await
                .expect("Copy Test File");

            let manager = DeploymentManager::new(tmp_dir)
                .await
                .expect("Can read and parse deployment");
            assert!(manager.valid_deployment.is_some())
        }
    }

    async fn read_file(path: impl AsRef<Path>) -> String {
        let err_msg = format!("Missing file {:#?}", path.as_ref());
        let mut file = File::open(path).await.expect(&err_msg);
        let mut contents = vec![];
        file.read_to_end(&mut contents).await.unwrap();

        String::from_utf8(contents).unwrap()
    }
}
