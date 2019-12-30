// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::convert::TryFrom;

use k8s_openapi::api::core::v1 as api_core;
use k8s_openapi::apimachinery::pkg::apis::meta::v1 as api_meta;

use crate::constants::{PULL_SECRET_DATA_NAME, PULL_SECRET_DATA_TYPE};
use crate::error::PullImageErrorReason;
use crate::error::Result;
use crate::registry::ImagePullSecret;
use crate::ErrorKind;

#[derive(Debug, PartialEq)]
pub struct NamedSecret(String, api_core::Secret);

impl NamedSecret {
    pub fn name(&self) -> &str {
        &self.0
    }

    pub fn secret(&self) -> &api_core::Secret {
        &self.1
    }
}

impl TryFrom<(String, ImagePullSecret)> for NamedSecret {
    type Error = crate::error::Error;

    fn try_from((namespace, image_pull_secret): (String, ImagePullSecret)) -> Result<Self> {
        let secret_name = image_pull_secret
            .name()
            .ok_or_else(|| ErrorKind::PullImage(PullImageErrorReason::AuthName))?;

        let mut secret_data = BTreeMap::new();
        secret_data.insert(PULL_SECRET_DATA_NAME.to_string(), image_pull_secret.data()?);

        Ok(NamedSecret(
            secret_name.clone(),
            api_core::Secret {
                data: Some(secret_data),
                metadata: Some(api_meta::ObjectMeta {
                    name: Some(secret_name),
                    namespace: Some(namespace),
                    ..api_meta::ObjectMeta::default()
                }),
                type_: Some(PULL_SECRET_DATA_TYPE.to_string()),
                ..api_core::Secret::default()
            },
        ))
    }
}

#[cfg(test)]
mod tests {
    use std::convert::TryFrom;

    use serde_json::{json, Value};

    use crate::constants::PULL_SECRET_DATA_TYPE;
    use crate::convert::named_secret::NamedSecret;
    use crate::error::PullImageErrorReason;
    use crate::registry::ImagePullSecret;
    use crate::ErrorKind;

    #[test]
    fn it_converts_to_named_secret() {
        let image_pull_secret = ImagePullSecret::default()
            .with_registry("REGISTRY")
            .with_username("USER")
            .with_password("PASSWORD");

        let pull_secret = NamedSecret::try_from(("namespace".into(), image_pull_secret)).unwrap();

        assert_eq!(pull_secret.name(), "user-registry".to_string());

        assert_eq!(
            pull_secret.secret().type_,
            Some(PULL_SECRET_DATA_TYPE.to_string())
        );
        assert!(pull_secret.secret().metadata.is_some());
        if let Some(meta) = pull_secret.secret().metadata.as_ref() {
            assert_eq!(meta.name, Some("user-registry".to_string()));
            assert_eq!(meta.namespace, Some("namespace".to_string()));
        }

        let json_data = json!({
            "auths": {
                "REGISTRY": {
                    "username":"USER",
                    "password":"PASSWORD",
                    "auth":"VVNFUjpQQVNTV09SRA=="
                }
            }
        });
        let actual_data: Value = serde_json::from_slice(
            pull_secret.secret().data.as_ref().unwrap()[".dockerconfigjson"]
                .0
                .as_slice(),
        )
        .unwrap();
        assert_eq!(actual_data, json_data);
    }

    #[test]
    fn it_fails_to_convert_to_named_secret_if_unable_generate_a_name_or_data() {
        let image_pull_secrets = vec![
            (
                ImagePullSecret::default(),
                ErrorKind::PullImage(PullImageErrorReason::AuthName),
            ),
            (
                ImagePullSecret::default().with_registry("REGISTRY"),
                ErrorKind::PullImage(PullImageErrorReason::AuthName),
            ),
            (
                ImagePullSecret::default()
                    .with_registry("REGISTRY")
                    .with_username("USER"),
                ErrorKind::PullImage(PullImageErrorReason::AuthPassword),
            ),
        ];

        for (image_pull_secret, cause) in image_pull_secrets {
            let err = NamedSecret::try_from(("namespace".into(), image_pull_secret)).unwrap_err();

            assert_eq!(err.kind(), &cause);
        }
    }
}
