// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use serde_derive::{Deserialize, Serialize};

use docker::models::{AuthConfig, ContainerCreateBody};
use edgelet_utils::{ensure_not_empty_with_context, serde_clone};

use crate::error::{ErrorKind, Result};

#[derive(Debug, Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct DockerConfig {
    image: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[serde(rename = "imageHash")]
    image_id: Option<String>,
    #[serde(default = "ContainerCreateBody::new")]
    create_options: ContainerCreateBody,
    #[serde(skip_serializing_if = "Option::is_none")]
    auth: Option<AuthConfig>,
}

impl DockerConfig {
    pub fn new(
        image: String,
        create_options: ContainerCreateBody,
        auth: Option<AuthConfig>,
    ) -> Result<Self> {
        ensure_not_empty_with_context(&image, || ErrorKind::InvalidImage(image.clone()))?;

        let config = DockerConfig {
            image,
            image_id: None,
            create_options,
            auth,
        };
        Ok(config)
    }

    pub fn clone_create_options(&self) -> Result<ContainerCreateBody> {
        Ok(serde_clone(&self.create_options).context(ErrorKind::CloneCreateOptions)?)
    }

    pub fn image(&self) -> &str {
        &self.image
    }

    pub fn with_image(mut self, image: String) -> Self {
        self.image = image;
        self
    }

    pub fn image_id(&self) -> Option<&str> {
        self.image_id.as_ref().map(AsRef::as_ref)
    }

    pub fn with_image_id(mut self, image_id: String) -> Self {
        self.image_id = Some(image_id);
        self
    }

    pub fn create_options(&self) -> &ContainerCreateBody {
        &self.create_options
    }

    pub fn with_create_options(mut self, create_options: ContainerCreateBody) -> Self {
        self.create_options = create_options;
        self
    }

    pub fn set_create_options(&mut self, create_options: ContainerCreateBody) {
        self.create_options = create_options;
    }

    pub fn auth(&self) -> Option<&AuthConfig> {
        self.auth.as_ref()
    }

    pub fn with_auth(mut self, auth: AuthConfig) -> Self {
        self.auth = Some(auth);
        self
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use docker::models::{ContainerCreateBody, HostConfig, HostConfigPortBindings};
    use serde_json::json;

    use super::*;

    #[test]
    #[should_panic]
    fn empty_image_fails() {
        DockerConfig::new("".to_string(), ContainerCreateBody::new(), None).unwrap();
    }

    #[test]
    #[should_panic]
    fn white_space_image_fails() {
        DockerConfig::new("    ".to_string(), ContainerCreateBody::new(), None).unwrap();
    }

    #[test]
    fn docker_config_ser() {
        let mut labels = HashMap::new();
        labels.insert("k1".to_string(), "v1".to_string());
        labels.insert("k2".to_string(), "v2".to_string());

        let mut port_bindings = HashMap::new();
        port_bindings.insert(
            "27017/tcp".to_string(),
            vec![HostConfigPortBindings::new().with_host_port("27017".to_string())],
        );

        let create_options = ContainerCreateBody::new()
            .with_host_config(HostConfig::new().with_port_bindings(port_bindings))
            .with_labels(labels);

        let config = DockerConfig::new("ubuntu".to_string(), create_options, None)
            .unwrap()
            .with_image_id("42".to_string());
        let actual_json = serde_json::to_string(&config).unwrap();
        let expected_json = json!({
            "image": "ubuntu",
            "imageHash": "42",
            "createOptions": {
                "Labels": {
                    "k1": "v1",
                    "k2": "v2"
                },
                "HostConfig": {
                    "PortBindings": {
                        "27017/tcp": [
                            {
                                "HostPort": "27017"
                            }
                        ]
                    }
                }
            }
        });
        assert_eq!(
            serde_json::from_str::<serde_json::Value>(&actual_json).unwrap(),
            expected_json
        );
    }

    #[test]
    fn docker_config_ser_auth() {
        let mut labels = HashMap::new();
        labels.insert("k1".to_string(), "v1".to_string());
        labels.insert("k2".to_string(), "v2".to_string());

        let mut port_bindings = HashMap::new();
        port_bindings.insert(
            "27017/tcp".to_string(),
            vec![HostConfigPortBindings::new().with_host_port("27017".to_string())],
        );

        let create_options = ContainerCreateBody::new()
            .with_host_config(HostConfig::new().with_port_bindings(port_bindings))
            .with_labels(labels);

        let auth_config = AuthConfig::new()
            .with_username("username".to_string())
            .with_password("password".to_string())
            .with_serveraddress("repo.azurecr.io".to_string());

        let config =
            DockerConfig::new("ubuntu".to_string(), create_options, Some(auth_config)).unwrap();
        let actual_json = serde_json::to_string(&config).unwrap();
        let expected_json = json!({
            "image": "ubuntu",
            "createOptions": {
                "Labels": {
                    "k1": "v1",
                    "k2": "v2"
                },
                "HostConfig": {
                    "PortBindings": {
                        "27017/tcp": [
                            {
                                "HostPort": "27017"
                            }
                        ]
                    }
                }
            },
            "auth": {
                "username": "username",
                "password": "password",
                "serveraddress": "repo.azurecr.io"
            }
        });
        assert_eq!(
            serde_json::from_str::<serde_json::Value>(&actual_json).unwrap(),
            expected_json
        );
    }

    #[test]
    fn docker_config_deser_no_create_options() {
        let input_json = json!({
            "image": "ubuntu"
        });
        let config = serde_json::from_str::<DockerConfig>(&input_json.to_string()).unwrap();
        assert_eq!(config.image, "ubuntu");
    }

    #[test]
    fn docker_config_deser_from_map() {
        let input_json = json!({
            "image": "ubuntu",
            "createOptions": {
                "Labels": {
                    "k1": "v1",
                    "k2": "v2"
                },
                "HostConfig": {
                    "PortBindings": {
                        "27017/tcp": [
                            {
                                "HostPort": "27017"
                            }
                        ]
                    }
                }
            },
            "auth": {
                "username": "username",
                "password": "password",
                "serveraddress": "repo.azurecr.io"
            }
        });

        let config = serde_json::from_str::<DockerConfig>(&input_json.to_string()).unwrap();
        assert_eq!(config.image, "ubuntu");
        assert_eq!(&config.create_options.labels().unwrap()["k1"], "v1");
        assert_eq!(&config.create_options.labels().unwrap()["k2"], "v2");

        let port_binding = &config
            .create_options
            .host_config()
            .unwrap()
            .port_bindings()
            .unwrap()["27017/tcp"];
        assert_eq!(
            port_binding.iter().next().unwrap().host_port().unwrap(),
            "27017"
        );

        assert_eq!("username", config.auth().unwrap().username().unwrap());
        assert_eq!("password", config.auth().unwrap().password().unwrap());
        assert_eq!(
            "repo.azurecr.io",
            config.auth().unwrap().serveraddress().unwrap()
        );
    }

    #[test]
    fn docker_config_deser_from_str() {
        let input_json = json!({
            "image": "ubuntu",
            "createOptions": {
                "Labels": {
                    "k1": "v1",
                    "k2": "v2"
                },
                "HostConfig": {
                    "PortBindings": {
                        "27017/tcp": [
                            {
                                "HostPort": "27017"
                            }
                        ]
                    }
                }
            }
        });

        let config: DockerConfig = serde_json::from_str(&input_json.to_string()).unwrap();
        assert_eq!(config.image, "ubuntu");
        assert_eq!(&config.create_options.labels().unwrap()["k1"], "v1");
        assert_eq!(&config.create_options.labels().unwrap()["k2"], "v2");

        let port_binding = &config
            .create_options
            .host_config()
            .unwrap()
            .port_bindings()
            .unwrap()["27017/tcp"];
        assert_eq!(
            port_binding.iter().next().unwrap().host_port().unwrap(),
            "27017"
        );
    }
}
