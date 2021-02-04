// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::path::PathBuf;

use url::Url;

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct MobyRuntime {
    pub(crate) uri: Url,
    pub(crate) network: MobyNetwork,
    pub(crate) content_trust: Option<ContentTrust>,
}

#[derive(Debug, serde_derive::Deserialize)]
#[serde(untagged)]
pub(crate) enum MobyNetwork {
    Network(Network),
    Name(String),
}

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct Network {
    pub(crate) name: String,

    #[serde(rename = "ipv6", skip_serializing_if = "Option::is_none")]
    pub(crate) ipv6: Option<bool>,

    #[serde(rename = "ipam", skip_serializing_if = "Option::is_none")]
    pub(crate) ipam: Option<Ipam>,
}

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct Ipam {
    #[serde(rename = "config", skip_serializing_if = "Option::is_none")]
    pub(crate) config: Option<Vec<IpamConfig>>,
}

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct IpamConfig {
    #[serde(rename = "gateway", skip_serializing_if = "Option::is_none")]
    pub(crate) gateway: Option<String>,

    #[serde(rename = "subnet", skip_serializing_if = "Option::is_none")]
    pub(crate) subnet: Option<String>,

    #[serde(rename = "ip_range", skip_serializing_if = "Option::is_none")]
    pub(crate) ip_range: Option<String>,
}

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct ContentTrust {
    pub(crate) ca_certs: Option<HashMap<String, PathBuf>>,
}
