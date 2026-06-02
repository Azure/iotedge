// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, serde::Serialize, serde::Deserialize)]
pub struct Ipam {
    #[serde(rename = "Config", skip_serializing_if = "Option::is_none")]
    pub config: Option<Vec<std::collections::BTreeMap<String, String>>>,
}
