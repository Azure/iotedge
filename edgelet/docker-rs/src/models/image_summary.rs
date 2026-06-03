// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct ImageSummary {
    #[serde(rename = "Id")]
    pub id: String,
    #[serde(rename = "RepoTags")]
    pub repo_tags: Option<Vec<String>>,
}
