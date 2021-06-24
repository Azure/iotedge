// Copyright (c) Microsoft. All rights reserved.

pub(super) mod create_or_list;
pub(super) mod delete_or_update;

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

impl std::convert::TryFrom<aziot_identity_common::Identity> for Identity {
    type Error = http_common::server::Error;

    fn try_from(identity: aziot_identity_common::Identity) -> Result<Self, Self::Error> {
        match identity {
            aziot_identity_common::Identity::Aziot(identity) => {
                let module_id = match identity.module_id {
                    Some(module_id) => module_id.0,
                    None => {
                        return Err(edgelet_http::error::server_error("missing module id"));
                    }
                };

                let generation_id = match identity.gen_id {
                    Some(gen_id) => gen_id.0,
                    None => {
                        return Err(edgelet_http::error::server_error("missing generation id"));
                    }
                };

                Ok(Identity {
                    module_id,
                    managed_by: default_managed_by(),
                    generation_id,
                    auth_type: "sas".to_string(),
                })
            }
            _ => Err(edgelet_http::error::server_error("invalid identity type")),
        }
    }
}

fn default_managed_by() -> String {
    "iotedge".to_string()
}
