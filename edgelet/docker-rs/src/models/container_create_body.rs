// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct ContainerCreateBody {
    #[serde(rename = "Hostname", skip_serializing_if = "Option::is_none")]
    pub hostname: Option<String>,
    #[serde(rename = "Env", skip_serializing_if = "Option::is_none")]
    pub env: Option<Vec<String>>,
    #[serde(rename = "Cmd", skip_serializing_if = "Option::is_none")]
    pub cmd: Option<Vec<String>>,
    #[serde(rename = "Image", skip_serializing_if = "Option::is_none")]
    pub image: Option<String>,
    #[serde(rename = "Volumes", skip_serializing_if = "Option::is_none")]
    pub volumes: Option<::std::collections::BTreeMap<String, serde_json::Value>>,
    #[serde(rename = "Entrypoint", skip_serializing_if = "Option::is_none")]
    pub entrypoint: Option<Vec<String>>,
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    pub labels: Option<::std::collections::BTreeMap<String, String>>,
    #[serde(rename = "HostConfig", skip_serializing_if = "Option::is_none")]
    pub host_config: Option<super::HostConfig>,
    #[serde(rename = "NetworkingConfig", skip_serializing_if = "Option::is_none")]
    pub networking_config: Option<ContainerCreateBodyNetworkingConfig>,
    #[serde(flatten)]
    pub other_properties: std::collections::BTreeMap<String, serde_json::Value>,
}

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct ContainerCreateBodyNetworkingConfig {
    #[serde(rename = "EndpointsConfig", skip_serializing_if = "Option::is_none")]
    pub endpoints_config: Option<::std::collections::BTreeMap<String, EndpointSettings>>,
    #[serde(flatten)]
    pub other_properties: std::collections::BTreeMap<String, serde_json::Value>,
}

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct EndpointSettings {
    #[serde(rename = "NetworkID", skip_serializing_if = "Option::is_none")]
    pub network_id: Option<String>,
    #[serde(flatten)]
    pub other_properties: std::collections::BTreeMap<String, serde_json::Value>,
}
