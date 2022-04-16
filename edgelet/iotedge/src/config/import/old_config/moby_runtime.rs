// Copyright (c) Microsoft. All rights reserved.

use url::Url;

#[derive(Debug, serde::Deserialize)]
pub(crate) struct MobyRuntime {
    pub(crate) uri: Url,
    pub(crate) network: MobyNetwork,
}

#[derive(Debug, serde::Deserialize)]
#[serde(untagged)]
pub(crate) enum MobyNetwork {
    Network(Network),
    Name(String),
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct Network {
    pub(crate) name: String,

    #[serde(rename = "ipv6", skip_serializing_if = "Option::is_none")]
    pub(crate) ipv6: Option<bool>,

    #[serde(rename = "ipam", skip_serializing_if = "Option::is_none")]
    pub(crate) ipam: Option<Ipam>,
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct Ipam {
    #[serde(rename = "config", skip_serializing_if = "Option::is_none")]
    pub(crate) config: Option<Vec<IpamConfig>>,
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct IpamConfig {
    #[serde(rename = "gateway", skip_serializing_if = "Option::is_none")]
    pub(crate) gateway: Option<String>,

    #[serde(rename = "subnet", skip_serializing_if = "Option::is_none")]
    pub(crate) subnet: Option<String>,

    #[serde(rename = "ip_range", skip_serializing_if = "Option::is_none")]
    pub(crate) ip_range: Option<String>,
}
