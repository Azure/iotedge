// Copyright (c) Microsoft. All rights reserved.

pub(super) mod create_or_list;

#[derive(Debug, serde::Deserialize, serde::Serialize)]
pub struct Identity {
    #[serde(rename = "moduleId")]
    pub module_id: String,
    #[serde(rename = "managedBy")]
    pub managed_by: String,
    #[serde(rename = "generationId")]
    pub generation_id: String,
    #[serde(rename = "authType")]
    pub auth_type: String,
}
