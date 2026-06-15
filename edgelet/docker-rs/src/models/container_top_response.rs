// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct ContainerTopResponse {
    #[serde(rename = "Titles", skip_serializing_if = "Option::is_none")]
    pub titles: Option<Vec<String>>,
    #[serde(rename = "Processes", skip_serializing_if = "Option::is_none")]
    pub processes: Option<Vec<Vec<String>>>,
}
