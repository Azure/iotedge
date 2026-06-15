// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct HostConfig {
    #[serde(rename = "Memory", skip_serializing_if = "Option::is_none")]
    pub memory: Option<i64>,
    #[serde(rename = "Binds", skip_serializing_if = "Option::is_none")]
    pub binds: Option<Vec<String>>,
    #[serde(rename = "PortBindings", skip_serializing_if = "Option::is_none")]
    pub port_bindings: Option<::std::collections::BTreeMap<String, Vec<HostConfigPortBindings>>>,
    #[serde(rename = "Mounts", skip_serializing_if = "Option::is_none")]
    pub mounts: Option<Vec<Mount>>,
    #[serde(rename = "CapAdd", skip_serializing_if = "Option::is_none")]
    pub cap_add: Option<Vec<String>>,
    #[serde(rename = "CapDrop", skip_serializing_if = "Option::is_none")]
    pub cap_drop: Option<Vec<String>>,
    #[serde(rename = "ExtraHosts", skip_serializing_if = "Option::is_none")]
    pub extra_hosts: Option<Vec<String>>,
    #[serde(rename = "Privileged", skip_serializing_if = "Option::is_none")]
    pub privileged: Option<bool>,
    #[serde(flatten)]
    pub other_properties: std::collections::BTreeMap<String, serde_json::Value>,
}

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct HostConfigPortBindings {
    #[serde(rename = "HostPort", skip_serializing_if = "Option::is_none")]
    pub host_port: Option<String>,
    #[serde(flatten)]
    pub other_properties: std::collections::BTreeMap<String, serde_json::Value>,
}

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct Mount {
    #[serde(rename = "Target", skip_serializing_if = "Option::is_none")]
    pub target: Option<String>,
    #[serde(rename = "Source", skip_serializing_if = "Option::is_none")]
    pub source: Option<String>,
    #[serde(rename = "Type", skip_serializing_if = "Option::is_none")]
    pub r#type: Option<String>,
    #[serde(rename = "ReadOnly", skip_serializing_if = "Option::is_none")]
    pub read_only: Option<bool>,
    #[serde(flatten)]
    pub other_properties: std::collections::BTreeMap<String, serde_json::Value>,
}
