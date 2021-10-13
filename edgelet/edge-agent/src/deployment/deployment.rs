use std::collections::HashMap;

// Made using https://transform.tools/json-to-rust-serde
// Currently not correct, only for testing purposes

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Deployment {
    pub properties: Properties,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Properties {
    pub desired: PropertiesInner,
    pub reported: PropertiesInner,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PropertiesInner {
    #[serde(default)]
    modules: HashMap<String, Module>,
    system_modules: SystemModules,
    runtime: Runtime,
    schema_version: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Module {
    settings: DockerConfig,
    r#type: RuntimeType,
    status: Option<String>,
    restart_policy: Option<String>,
    version: Option<String>,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SystemModules {
    edge_hub: Module,
    edge_agent: Module,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Runtime {
    settings: RuntimeSettings,
    r#type: RuntimeType,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeSettings {
    min_docker_version: String,
}

#[derive(Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub enum RuntimeType {
    #[serde(rename = "docker")]
    Docker,
}

impl Default for RuntimeType {
    fn default() -> Self {
        RuntimeType::Docker
    }
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DockerConfig {
    image: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    image_hash: Option<String>,

    #[serde(
        deserialize_with = "deserialize_create_options",
        skip_serializing_if = "Option::is_none",
        default
    )]
    create_options: Option<docker::models::ContainerCreateBody>,

    #[serde(skip_serializing_if = "Option::is_none")]
    digest: Option<String>,

    #[serde(skip_serializing_if = "Option::is_none")]
    auth: Option<docker::models::AuthConfig>,

    #[serde(
        default = "edgelet_settings::base::default_allow_elevated_docker_permissions",
        skip_serializing
    )]
    allow_elevated_docker_permissions: bool,
}

fn deserialize_create_options<'de, D>(
    deserializer: D,
) -> Result<Option<docker::models::ContainerCreateBody>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let s: String = serde::de::Deserialize::deserialize(deserializer)?;
    if s.is_empty() {
        Ok(None)
    } else {
        serde_json::from_str(&s).map_err(serde::de::Error::custom)
    }
}
