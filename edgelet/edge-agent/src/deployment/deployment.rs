use std::{
    collections::{BTreeMap, HashMap},
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
    pub status: Option<String>,
    pub restart_policy: Option<String>,
    pub image_pull_policy: Option<String>,
    pub version: Option<String>,
    #[serde(default)]
    pub env: BTreeMap<String, String>,
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

    #[serde(flatten)]
    create_option: CreateOption,

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

impl From<DockerSettings> for edgelet_settings::DockerConfig {
    fn from(settings: DockerSettings) -> Self {
        Self::default() // TODO: convert
    }
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize)]
pub struct CreateOption {
    #[serde(
        skip_serializing_if = "Option::is_none",
        default,
        rename = "createOptions"
    )]
    create_options: Option<docker::models::ContainerCreateBody>,
}

impl<'de> serde::Deserialize<'de> for CreateOption {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        // enum Field { Secs, Nanos }

        // // This part could also be generated independently by:
        // //
        // #[derive(serde::Deserialize)]
        // #[serde(field_identifier, rename_all = "lowercase")]
        // enum Field {
        //     Secs,
        //     Nanos,
        // }
        // impl<'de> Deserialize<'de> for Field {
        //     fn deserialize<D>(deserializer: D) -> Result<Field, D::Error>
        //     where
        //         D: Deserializer<'de>,
        //     {
        //         struct FieldVisitor;

        //         impl<'de> Visitor<'de> for FieldVisitor {
        //             type Value = Field;

        //             fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
        //                 formatter.write_str("`secs` or `nanos`")
        //             }

        //             fn visit_str<E>(self, value: &str) -> Result<Field, E>
        //             where
        //                 E: de::Error,
        //             {
        //                 match value {
        //                     "secs" => Ok(Field::Secs),
        //                     "nanos" => Ok(Field::Nanos),
        //                     _ => Err(de::Error::unknown_field(value, FIELDS)),
        //                 }
        //             }
        //         }

        //         deserializer.deserialize_identifier(FieldVisitor)
        //     }
        // }

        struct CreateOptionsVisitor;

        impl<'de> serde::de::Visitor<'de> for CreateOptionsVisitor {
            type Value = CreateOption;

            fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
                formatter.write_str("struct Duration")
            }

            fn visit_map<V>(self, mut map: V) -> Result<Self::Value, V::Error>
            where
                V: serde::de::MapAccess<'de>,
            {
                println!("visit map");
                let mut create_options: Option<docker::models::ContainerCreateBody> = None;
                let mut parts: [String; 10];

                while let Some(key) = map.next_key::<String>()? {
                    let s: String = map.next_value()?;
                    println!("Got key {}: {}", key, s);

                    if key == "createOptions" {
                        if create_options.is_some() {
                            return Err(serde::de::Error::duplicate_field("createOptions"));
                        }

                        create_options = if s.is_empty() {
                            None
                        } else {
                            Some(serde_json::from_str(&s).map_err(serde::de::Error::custom)?)
                        };
                    }
                }
                // let secs = secs.ok_or_else(|| de::Error::missing_field("secs"))?;
                // let nanos = nanos.ok_or_else(|| de::Error::missing_field("nanos"))?;

                Ok(CreateOption { create_options })
            }
        }

        const FIELDS: &'static [&'static str] = &["createOptions"];
        deserializer.deserialize_struct("CreateOption", FIELDS, CreateOptionsVisitor)
    }
}

fn deserialize_create_options<'de, D>(
    deserializer: D,
) -> Result<Option<docker::models::ContainerCreateBody>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let s: String = serde::de::Deserialize::deserialize(deserializer)?;
    if s.is_empty() {
        // println!("Parsing empty create options");
        Ok(None)
    } else {
        // println!("Parsing create options: {}", s);
        serde_json::from_str(&s).map_err(serde::de::Error::custom)
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
        let test_file = std::path::Path::new(concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/src/deployment/test/twin1.json"
        ));
        let raw_deployment = File::open(&test_file).unwrap();
        let deployment: Deployment = serde_json::from_reader(&raw_deployment)
            .expect(&format!("Could not parse deployment file {:#?}", test_file));

        assert!(
            deployment
                .properties
                .desired
                .system_modules
                .edge_hub
                .settings
                .create_option
                .create_options
                != None
        );
    }
}
