// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct NetworkConfig {
    #[serde(rename = "Name")]
    pub name: String,
    #[serde(rename = "IPAM", skip_serializing_if = "Option::is_none")]
    pub ipam: Option<crate::models::Ipam>,
    #[serde(rename = "EnableIPv6", skip_serializing_if = "Option::is_none")]
    pub enable_ipv6: Option<bool>,
}
