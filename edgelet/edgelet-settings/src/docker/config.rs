// Copyright (c) Microsoft. All rights reserved.
pub const UPSTREAM_PARENT_KEYWORD: &str = "$upstream";

#[derive(Clone, Debug, Default, PartialEq, serde::Serialize, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DockerConfig {
    image: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    image_hash: Option<String>,

    #[serde(default = "docker::models::ContainerCreateBody::new")]
    create_options: docker::models::ContainerCreateBody,

    #[serde(skip_serializing_if = "Option::is_none")]
    digest: Option<String>,

    #[serde(skip_serializing_if = "Option::is_none")]
    auth: Option<docker::models::AuthConfig>,

    #[serde(
        default = "crate::base::default_allow_elevated_docker_permissions",
        skip_serializing
    )]
    allow_elevated_docker_permissions: bool,
}

impl DockerConfig {
    pub fn new(
        image: String,
        create_options: docker::models::ContainerCreateBody,
        digest: Option<String>,
        auth: Option<docker::models::AuthConfig>,
        allow_elevated_docker_permissions: bool,
    ) -> Result<Self, String> {
        if image.trim().is_empty() {
            return Err("image cannot be empty".to_string());
        }

        Ok(DockerConfig {
            image,
            image_hash: None,
            create_options,
            digest,
            auth,
            allow_elevated_docker_permissions,
        })
    }

    pub fn image(&self) -> &str {
        &self.image
    }

    pub fn with_image(mut self, image: String) -> Self {
        self.image = image;
        self
    }

    pub fn image_hash(&self) -> Option<&str> {
        self.image_hash.as_deref()
    }

    pub fn with_image_hash(mut self, image_id: String) -> Self {
        self.image_hash = Some(image_id);
        self
    }

    pub fn create_options(&self) -> &docker::models::ContainerCreateBody {
        &self.create_options
    }

    pub fn create_options_mut(&mut self) -> &mut docker::models::ContainerCreateBody {
        &mut self.create_options
    }

    pub fn with_create_options(
        mut self,
        create_options: docker::models::ContainerCreateBody,
    ) -> Self {
        self.create_options = create_options;
        self
    }

    pub fn set_create_options(&mut self, create_options: docker::models::ContainerCreateBody) {
        self.create_options = create_options;
    }

    pub fn digest(&self) -> Option<&str> {
        self.digest.as_deref()
    }

    pub fn auth(&self) -> Option<&docker::models::AuthConfig> {
        self.auth.as_ref()
    }

    pub fn with_auth(mut self, auth: docker::models::AuthConfig) -> Self {
        self.auth = Some(auth);
        self
    }

    pub fn allow_elevated_docker_permissions(&self) -> bool {
        self.allow_elevated_docker_permissions
    }

    pub fn parent_hostname_resolve(&mut self, parent_hostname: &str) {
        if let Some(rest) = self.image.strip_prefix(UPSTREAM_PARENT_KEYWORD) {
            self.image = format!("{}{}", parent_hostname, rest);
        }

        let auth = match &self.auth {
            Some(auth) => auth,
            _ => return,
        };

        if let Some(serveraddress) = auth.serveraddress() {
            if let Some(rest) = serveraddress.strip_prefix(UPSTREAM_PARENT_KEYWORD) {
                let url = rest.to_string();
                if let Some(auth) = &mut self.auth {
                    auth.set_serveraddress(format!("{}{}", parent_hostname, url));
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use docker::models::{AuthConfig, ContainerCreateBody, HostConfig, HostConfigPortBindings};
    use serde_json::json;

    use super::DockerConfig;

    #[test]
    fn empty_image_fails() {
        DockerConfig::new("".to_string(), ContainerCreateBody::new(), None, None, true)
            .unwrap_err();
    }

    #[test]
    fn white_space_image_fails() {
        DockerConfig::new(
            "    ".to_string(),
            ContainerCreateBody::new(),
            None,
            None,
            true,
        )
        .unwrap_err();
    }

    #[test]
    fn docker_config_ser() {
        let mut labels = std::collections::BTreeMap::new();
        labels.insert("k1".to_string(), "v1".to_string());
        labels.insert("k2".to_string(), "v2".to_string());

        let mut port_bindings = std::collections::BTreeMap::new();
        port_bindings.insert(
            "27017/tcp".to_string(),
            vec![HostConfigPortBindings::new().with_host_port("27017".to_string())],
        );

        let create_options = ContainerCreateBody::new()
            .with_host_config(HostConfig::new().with_port_bindings(port_bindings))
            .with_labels(labels);

        let config = DockerConfig::new("ubuntu".to_string(), create_options, None, None, true)
            .unwrap()
            .with_image_hash("42".to_string());
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
        let mut labels = std::collections::BTreeMap::new();
        labels.insert("k1".to_string(), "v1".to_string());
        labels.insert("k2".to_string(), "v2".to_string());

        let mut port_bindings = std::collections::BTreeMap::new();
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

        let config = DockerConfig::new(
            "ubuntu".to_string(),
            create_options,
            None,
            Some(auth_config),
            true,
        )
        .unwrap();
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
