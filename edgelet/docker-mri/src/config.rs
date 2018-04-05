// Copyright (c) Microsoft. All rights reserved.

use docker::models::ContainerCreateBody;
use edgelet_utils::{serde_clone, string_or_struct};

use error::Result;

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DockerConfig {
    image: String,
    #[serde(deserialize_with = "string_or_struct")]
    create_options: ContainerCreateBody,
}

impl DockerConfig {
    pub fn new(image: &str, create_options: ContainerCreateBody) -> Result<DockerConfig> {
        Ok(DockerConfig {
            image: ensure_not_empty!(image.to_string()),
            create_options,
        })
    }

    pub fn clone_create_options(&self) -> Result<ContainerCreateBody> {
        Ok(serde_clone(&self.create_options)?)
    }

    pub fn image(&self) -> &str {
        &self.image
    }

    pub fn create_options(&self) -> &ContainerCreateBody {
        &self.create_options
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use serde_json;
    use docker::models::{ContainerCreateBody, HostConfig, HostConfigPortBindings};

    use config::DockerConfig;

    #[test]
    #[should_panic]
    fn empty_image_fails1() {
        DockerConfig::new("", ContainerCreateBody::new()).unwrap();
    }

    #[test]
    #[should_panic]
    fn empty_image_fails2() {
        DockerConfig::new("    ", ContainerCreateBody::new()).unwrap();
    }

    #[test]
    fn docker_config_ser() {
        let mut labels = HashMap::new();
        labels.insert("k1".to_string(), "v1".to_string());
        labels.insert("k2".to_string(), "v2".to_string());

        let mut port_bindings = HashMap::new();
        port_bindings.insert(
            "27017/tcp".to_string(),
            HostConfigPortBindings::new().with_host_port("27017".to_string()),
        );

        let create_options = ContainerCreateBody::new()
            .with_host_config(HostConfig::new().with_port_bindings(port_bindings))
            .with_labels(labels);

        let config = DockerConfig::new("ubuntu", create_options).unwrap();
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
						"27017/tcp": {
							"HostPort": "27017"
						}
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
						"27017/tcp": {
							"HostPort": "27017"
						}
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
        assert_eq!(port_binding.host_port().unwrap(), "27017");
    }

    #[test]
    fn docker_config_deser_from_str() {
        let input_json = json!({
			"image": "ubuntu",
			"createOptions": json!({
				"Labels": {
					"k1": "v1",
					"k2": "v2"
				},
				"HostConfig": {
					"PortBindings": {
						"27017/tcp": {
							"HostPort": "27017"
						}
					}
				}
			}).to_string()
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
        assert_eq!(port_binding.host_port().unwrap(), "27017");
    }
}
