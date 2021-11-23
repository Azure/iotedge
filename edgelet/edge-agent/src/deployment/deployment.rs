use std::{
    collections::{BTreeMap, HashMap},
    convert::TryFrom,
    fmt,
};

// https://github.com/SchemaStore/schemastore/blob/master/src/schemas/json/azure-iot-edgeagent-deployment-1.1.json

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Deployment {
    pub properties: Properties,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Properties {
    pub desired: PropertiesInner,
    pub reported: serde_json::Value,
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
    #[serde(default)]
    pub status: ModuleStatus,
    #[serde(default)]
    pub restart_policy: RestartPolicy,
    #[serde(default)]
    pub image_pull_policy: edgelet_settings::module::ImagePullPolicy,
    pub version: Option<String>,
    #[serde(default)]
    pub env: BTreeMap<String, EnvValue>,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SystemModules {
    pub edge_hub: ModuleConfig,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub edge_agent: Option<ModuleConfig>,
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
#[serde(rename_all = "lowercase")]
pub enum RestartPolicy {
    Always,
    #[serde(rename = "on-failure")]
    OnFailure,
    #[serde(rename = "on-unhealthy")]
    OnUnhealthy,
    Never,
}

impl std::fmt::Display for RestartPolicy {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Always => f.write_str("always"),
            Self::OnFailure => f.write_str("on-failure"),
            Self::OnUnhealthy => f.write_str("on-unhealthy"),
            Self::Never => f.write_str("never"),
        }
    }
}

impl Default for RestartPolicy {
    fn default() -> Self {
        Self::Always
    }
}

#[derive(Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum RuntimeType {
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
        Self::Docker
    }
}

#[derive(Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum ModuleStatus {
    Running,
    Stopped,
}

impl std::fmt::Display for ModuleStatus {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Running => f.write_str("running"),
            Self::Stopped => f.write_str("stopped"),
        }
    }
}

impl Default for ModuleStatus {
    fn default() -> Self {
        Self::Running
    }
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DockerSettings {
    pub image: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub image_hash: Option<String>,

    #[serde(flatten)]
    pub create_option: CreateOption,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub digest: Option<String>,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub auth: Option<docker::models::AuthConfig>,

    #[serde(
        default = "edgelet_settings::base::default_allow_elevated_docker_permissions",
        skip_serializing_if = "is_default_docker_perms"
    )]
    pub allow_elevated_docker_permissions: bool,
}

fn is_default_docker_perms(val: &bool) -> bool {
    val == &edgelet_settings::base::default_allow_elevated_docker_permissions()
}

impl TryFrom<ModuleConfig> for edgelet_settings::DockerConfig {
    type Error = Box<dyn std::error::Error + Send + Sync + 'static>;

    fn try_from(config: ModuleConfig) -> Result<Self, Self::Error> {
        let ModuleConfig { settings, env, .. } = config;

        // Merge env variables
        let create_options = settings.create_option.create_options.unwrap_or_default();
        let env = env
            .iter()
            .map(|(k, v)| (k.to_owned(), v.to_string()))
            .collect();
        let env = docker::utils::merge_env(create_options.env(), &env);

        let create_options = if env.is_empty() {
            create_options
        } else {
            create_options.with_env(env)
        };

        let config = edgelet_settings::DockerConfig::new(
            settings.image,
            create_options,
            settings.digest,
            settings.auth,
            settings.allow_elevated_docker_permissions,
        )?;

        Ok(config)
    }
}

#[derive(Default, Debug, Clone, PartialEq)]
pub struct CreateOption {
    pub create_options: Option<docker::models::ContainerCreateBody>,
}

lazy_static::lazy_static! {
    static ref CREATE_OPTIONS_REGEX: regex::Regex = regex::Regex::new(r"^(createoptions|createOptions|CreateOptions)(?P<index>\d*)$").expect("could not compile regex");
}

impl<'de> serde::Deserialize<'de> for CreateOption {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        struct CreateOptionsVisitor;

        impl<'de> serde::de::Visitor<'de> for CreateOptionsVisitor {
            type Value = CreateOption;

            fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
                formatter.write_str("create options")
            }

            fn visit_map<V>(self, mut map: V) -> Result<Self::Value, V::Error>
            where
                V: serde::de::MapAccess<'de>,
            {
                let mut create_options: String = String::new();
                let mut parts: Vec<(u32, String)> = Vec::new();

                while let Some(key) = map.next_key::<String>()? {
                    if let Some(capture) = CREATE_OPTIONS_REGEX.captures(&key) {
                        let value: String = map.next_value()?;

                        let index = &capture["index"];
                        if index.is_empty() {
                            // this is the normal case of just using createOptions
                            create_options = value;
                        } else {
                            // this is the case of using createOptions1, createOptions2, etc.
                            let index: u32 = index.parse().map_err(serde::de::Error::custom)?;
                            parts.push((index, value));
                        };
                    }
                }

                if !parts.is_empty() {
                    parts.sort_by(|a, b| a.0.partial_cmp(&b.0).unwrap());
                    for (_i, part) in parts {
                        create_options += &part;
                    }
                }

                let create_options = if create_options.is_empty() {
                    None
                } else {
                    Some(serde_json::from_str(&create_options).map_err(serde::de::Error::custom)?)
                };

                Ok(CreateOption { create_options })
            }
        }
        deserializer.deserialize_map(CreateOptionsVisitor)
    }
}

impl serde::Serialize for CreateOption {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        if let Some(create_options) = &self.create_options {
            let string_wrapper: String = serde_json::to_string(create_options).unwrap();
            serializer.collect_map([("createOptions", string_wrapper)])
        } else {
            serializer.serialize_none()
        }
    }
}

#[derive(Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
#[serde(untagged)]
pub enum EnvValue {
    Number(f64),
    Bool(bool),
    String(String),
}

impl fmt::Display for EnvValue {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            EnvValue::Number(v) => f.write_str(&v.to_string()),
            EnvValue::Bool(v) => f.write_str(&v.to_string()),
            EnvValue::String(v) => f.write_str(v),
        }
    }
}

#[cfg(test)]
mod tests {
    use std::fs::File;

    use super::*;

    #[test]
    fn test_read_all() {
        let test_files_directory =
            std::path::Path::new(concat!(env!("CARGO_MANIFEST_DIR"), "/src/deployment/test"));

        for test_file in std::fs::read_dir(test_files_directory).unwrap() {
            let test_file = test_file.unwrap();
            if test_file.file_type().unwrap().is_dir() {
                continue;
            }
            let test_file = test_file.path();

            println!("Parsing deployment file {:#?}", test_file);
            let raw_deployment = File::open(&test_file).unwrap();
            let _deployment: Deployment = serde_json::from_reader(&raw_deployment)
                .expect(&format!("Could not parse deployment file {:#?}", test_file));
        }
    }

    #[test]
    fn test_parse_create_options() {
        for file in ["twin1", "twin1_split_create_options"] {
            let test_file = format!(
                "{}/src/deployment/test/{}.json",
                env!("CARGO_MANIFEST_DIR"),
                file
            );
            let raw_deployment = File::open(&test_file).unwrap();
            let deployment: Deployment = serde_json::from_reader(&raw_deployment)
                .expect(&format!("Could not parse deployment file {:#?}", test_file));

            let create_options = deployment
                .properties
                .desired
                .system_modules
                .edge_hub
                .settings
                .create_option
                .create_options;
            let body = create_options.expect(&format!("create_options missing for {}", file));
            let _host_config = body
                .host_config()
                .expect(&format!("host_config missing for {}", file));
        }
    }
}
