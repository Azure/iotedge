// Made using https://transform.tools/json-to-rust-serde
// Currently not correct, only for testing purposes

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Deployment {
    #[serde(rename = "modulesContent")]
    pub modules_content: ModulesContent,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct ModulesContent {
    #[serde(rename = "$edgeAgent")]
    pub edge_agent: EdgeAgent,
    #[serde(rename = "$edgeHub")]
    pub edge_hub: EdgeHub2,
    #[serde(rename = "ComponentNameTest")]
    pub component_name_test: ComponentNameTest2,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct EdgeAgent {
    #[serde(rename = "properties.desired")]
    pub properties_desired: EdgeAgentPropertiesDesired,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct EdgeAgentPropertiesDesired {
    pub modules: Modules,
    pub runtime: Runtime,
    #[serde(rename = "schemaVersion")]
    pub schema_version: String,
    #[serde(rename = "systemModules")]
    pub system_modules: SystemModules,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Modules {
    #[serde(rename = "ComponentNameTest")]
    pub component_name_test: ComponentNameTest,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct ComponentNameTest {
    pub settings: Settings,
    #[serde(rename = "type")]
    pub type_field: String,
    pub version: String,
    pub status: String,
    #[serde(rename = "restartPolicy")]
    pub restart_policy: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Settings {
    pub image: String,
    #[serde(rename = "createOptions")]
    pub create_options: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Runtime {
    pub settings: Settings2,
    #[serde(rename = "type")]
    pub type_field: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Settings2 {
    #[serde(rename = "minDockerVersion")]
    pub min_docker_version: String,
    #[serde(rename = "registryCredentials")]
    pub registry_credentials: RegistryCredentials,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct RegistryCredentials {
    pub edgebuilds: Edgebuilds,
    pub lefitchereg1: Lefitchereg1,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Edgebuilds {
    pub address: String,
    pub password: String,
    pub username: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Lefitchereg1 {
    pub address: String,
    pub password: String,
    pub username: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct SystemModules {
    #[serde(rename = "edgeAgent")]
    pub edge_agent: EdgeAgent2,
    #[serde(rename = "edgeHub")]
    pub edge_hub: EdgeHub,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct EdgeAgent2 {
    pub settings: Settings3,
    #[serde(rename = "type")]
    pub type_field: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Settings3 {
    pub image: String,
    #[serde(rename = "createOptions")]
    pub create_options: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct EdgeHub {
    pub settings: Settings4,
    #[serde(rename = "type")]
    pub type_field: String,
    pub env: Env,
    pub status: String,
    #[serde(rename = "restartPolicy")]
    pub restart_policy: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Settings4 {
    pub image: String,
    #[serde(rename = "createOptions")]
    pub create_options: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Env {
    #[serde(rename = "UpstreamProtocol")]
    pub upstream_protocol: UpstreamProtocol,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct UpstreamProtocol {
    pub value: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct EdgeHub2 {
    #[serde(rename = "properties.desired")]
    pub properties_desired: PropertiesDesired2,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct PropertiesDesired2 {
    pub routes: Routes,
    #[serde(rename = "schemaVersion")]
    pub schema_version: String,
    #[serde(rename = "storeAndForwardConfiguration")]
    pub store_and_forward_configuration: StoreAndForwardConfiguration,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Routes {
    #[serde(rename = "All")]
    pub all: String,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct StoreAndForwardConfiguration {
    #[serde(rename = "timeToLiveSecs")]
    pub time_to_live_secs: i64,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct ComponentNameTest2 {
    #[serde(rename = "properties.desired")]
    pub properties_desired: PropertiesDesired3,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct PropertiesDesired3 {
}
