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

impl std::convert::TryFrom<aziot_identity_common::Identity> for Identity {
    type Error = http_common::server::Error;

    fn try_from(identity: aziot_identity_common::Identity) -> Result<Self, Self::Error> {
        match identity {
            aziot_identity_common::Identity::Aziot(identity) => {
                let module_id = match identity.module_id {
                    Some(module_id) => module_id.0,
                    None => {
                        return Err(http_common::server::Error {
                            status_code: http::StatusCode::INTERNAL_SERVER_ERROR,
                            message: "missing module id".into(),
                        });
                    }
                };

                let generation_id = match identity.gen_id {
                    Some(gen_id) => gen_id.0,
                    None => {
                        return Err(http_common::server::Error {
                            status_code: http::StatusCode::INTERNAL_SERVER_ERROR,
                            message: "missing generation id".into(),
                        });
                    }
                };

                Ok(Identity {
                    module_id,
                    managed_by: "iotedge".to_string(),
                    generation_id,
                    auth_type: "sas".to_string(),
                })
            }
            _ => Err(http_common::server::Error {
                status_code: http::StatusCode::INTERNAL_SERVER_ERROR,
                message: "invalid identity type".into(),
            }),
        }
    }
}
