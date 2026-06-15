// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct AuthConfig {
    #[serde(rename = "username", skip_serializing_if = "Option::is_none")]
    pub username: Option<String>,
    #[serde(rename = "password", skip_serializing_if = "Option::is_none")]
    pub password: Option<String>,
    #[serde(rename = "email", skip_serializing_if = "Option::is_none")]
    pub email: Option<String>,
    #[serde(rename = "serveraddress", skip_serializing_if = "Option::is_none")]
    pub server_address: Option<String>,
    #[serde(flatten)]
    pub other_properties: std::collections::BTreeMap<String, serde_json::Value>,
}
