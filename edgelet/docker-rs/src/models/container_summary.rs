// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct ContainerSummary {
    #[serde(rename = "Names")]
    pub names: Vec<String>,
    #[serde(rename = "Image")]
    pub image: String,
    #[serde(rename = "ImageID")]
    pub image_id: String,
    #[serde(rename = "Labels")]
    pub labels: std::collections::BTreeMap<String, String>,
}
