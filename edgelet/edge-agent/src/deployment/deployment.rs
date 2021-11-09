use std::collections::{BTreeMap, HashMap};

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
    pub modules: HashMap<String, ModuleConfig>,
    pub system_modules: SystemModules,
    pub runtime: Runtime,
    pub schema_version: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ModuleConfig {
    pub settings: DockerSettings,
    pub r#type: RuntimeType,
    pub env: BTreeMap<String, String>,
    pub status: Option<String>,
    pub restart_policy: Option<String>,
    pub imagePullPolicy: Option<String>,
    pub version: Option<String>,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SystemModules {
    pub edge_hub: ModuleConfig,
    pub edge_agent: ModuleConfig,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Runtime {
    pub settings: RuntimeSettings,
    pub r#type: RuntimeType,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeSettings {
    pub min_docker_version: String,
}

#[derive(Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub enum RuntimeType {
    #[serde(rename = "docker")]
    Docker,
}

impl std::fmt::Display for RuntimeType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Docker => f.write_str("docker"),
        }
    }
}

impl Default for RuntimeType {
    fn default() -> Self {
        RuntimeType::Docker
    }
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DockerSettings {
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
