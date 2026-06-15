// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, Default, serde::Serialize, serde::Deserialize)]
pub struct SystemInfo {
    #[serde(rename = "ServerVersion", skip_serializing_if = "Option::is_none")]
    pub server_version: Option<String>,
}
