// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::str;

use base64;
use docker::models::AuthConfig;
use failure::ResultExt;
use k8s_openapi::ByteString;
use serde_json;

use crate::error::{ErrorKind, PullImageErrorReason, Result};

#[derive(Debug, PartialEq, Default)]
pub struct ImagePullSecret {
    registry: Option<String>,
    username: Option<String>,
    password: Option<String>,
}

impl ImagePullSecret {
    pub fn from_auth(auth: &AuthConfig) -> Option<Self> {
        if let (None, None, None) = (auth.serveraddress(), auth.username(), auth.password()) {
            None
        } else {
            Some(Self {
                registry: auth.serveraddress().map(Into::into),
                username: auth.username().map(Into::into),
                password: auth.password().map(Into::into),
            })
        }
    }

    #[cfg(test)]
    pub fn with_registry(mut self, registry: impl Into<String>) -> Self {
        self.registry = Some(registry.into());
        self
    }

    #[cfg(test)]
    pub fn with_username(mut self, username: impl Into<String>) -> Self {
        self.username = Some(username.into());
        self
    }

    #[cfg(test)]
    pub fn with_password(mut self, password: impl Into<String>) -> Self {
        self.password = Some(password.into());
        self
    }

    pub fn name(&self) -> Option<String> {
        match (&self.username, &self.registry) {
            (Some(user), Some(registry)) => Some(format!(
                "{}-{}",
                user.to_lowercase(),
                registry.to_lowercase()
            )),
            _ => None,
        }
    }

    pub fn data(&self) -> Result<ByteString> {
        let registry = self
            .registry
            .as_ref()
            .ok_or_else(|| ErrorKind::PullImage(PullImageErrorReason::AuthServerAddress))?;

        let user = self
            .username
            .as_ref()
            .ok_or_else(|| ErrorKind::PullImage(PullImageErrorReason::AuthUser))?;

        let password = self
            .password
            .as_ref()
            .ok_or_else(|| ErrorKind::PullImage(PullImageErrorReason::AuthPassword))?;

        let mut auths = BTreeMap::new();
        auths.insert(
            registry.to_string(),
            AuthEntry::new(user.to_string(), password.to_string()),
        );

        Auth::new(auths).secret_data()
    }
}

// AuthEntry models the JSON string needed for entryies in the image pull secrets.
#[derive(Debug, serde_derive::Serialize, serde_derive::Deserialize, Clone)]
struct AuthEntry {
    pub username: String,
    pub password: String,
    pub auth: String,
}

impl AuthEntry {
    pub fn new(username: String, password: String) -> AuthEntry {
        let auth = base64::encode(&format!("{}:{}", username, password));
        AuthEntry {
            username,
            password,
            auth,
        }
    }
}

// Auth represents the JSON string needed for image pull secrets.
// JSON struct is
// { "auths":
//   {"<registry>" :
//      { "username":"<user>",
//        "password":"<password>",
//        "email":"<email>" (not needed)
//        "auth":"<base64 of '<user>:<password>'>"
//       }
//   }
// }
#[derive(Debug, serde_derive::Serialize, serde_derive::Deserialize, Clone)]
struct Auth {
    pub auths: BTreeMap<String, AuthEntry>,
}

impl Auth {
    pub fn new(auths: BTreeMap<String, AuthEntry>) -> Auth {
        Auth { auths }
    }

    pub fn secret_data(&self) -> Result<ByteString> {
        let data =
            serde_json::to_vec(self).context(ErrorKind::PullImage(PullImageErrorReason::Json))?;
        Ok(ByteString(data))
    }
}

#[cfg(test)]
mod tests {
    use docker::models::AuthConfig;
    use serde_json::{json, Value};

    use crate::error::PullImageErrorReason;
    use crate::registry::ImagePullSecret;
    use crate::ErrorKind;

    #[test]
    fn it_converts_to_image_pull_secret_none_when_all_data_missing() {
        let auth = AuthConfig::new();

        let image_pull_secret = ImagePullSecret::from_auth(&auth);

        assert!(image_pull_secret.is_none());
    }

    #[test]
    fn it_converts_to_image_pull_secret_some_if_any_data_exist() {
        let auths = vec![
            AuthConfig::new()
                .with_username(String::from("USER"))
                .with_serveraddress(String::from("REGISTRY")),
            AuthConfig::new()
                .with_password(String::from("a password"))
                .with_serveraddress(String::from("REGISTRY")),
            AuthConfig::new()
                .with_password(String::from("a password"))
                .with_username(String::from("USER")),
        ];

        for auth in auths {
            let image_pull_secret = ImagePullSecret::from_auth(&auth);
            assert!(image_pull_secret.is_some());
        }
    }

    #[test]
    fn it_returns_some_secret_name() {
        let image_pull_secret = ImagePullSecret::default()
            .with_registry("REGISTRY")
            .with_username("USER");

        let name = image_pull_secret.name();

        assert_eq!(name, Some("user-registry".to_string()));
    }

    #[test]
    fn it_returns_none_secret_name_when_username_or_registry_missing() {
        let image_pull_secrets = vec![
            ImagePullSecret::default().with_registry("REGISTRY"),
            ImagePullSecret::default().with_username("USER"),
            ImagePullSecret::default(),
        ];

        for image_pull_secret in image_pull_secrets {
            let name = image_pull_secret.name();

            assert_eq!(name, None);
        }
    }

    #[test]
    fn it_generates_secret_data() {
        let image_pull_secret = ImagePullSecret::default()
            .with_registry("REGISTRY")
            .with_username("USER")
            .with_password("PASSWORD");

        let data = image_pull_secret.data();

        let expected = json!({
            "auths": {
                "REGISTRY": {
                    "username":"USER",
                    "password":"PASSWORD",
                    "auth":"VVNFUjpQQVNTV09SRA=="
                }
            }
        });
        let actual: Value = serde_json::from_slice(data.unwrap().0.as_slice()).unwrap();
        assert_eq!(actual, expected);
    }

    #[test]
    fn it_fails_to_generate_secret_data() {
        let image_pull_secrets = vec![
            (
                ImagePullSecret::default(),
                ErrorKind::PullImage(PullImageErrorReason::AuthServerAddress),
            ),
            (
                ImagePullSecret::default().with_registry("REGISTRY"),
                ErrorKind::PullImage(PullImageErrorReason::AuthUser),
            ),
            (
                ImagePullSecret::default()
                    .with_registry("REGISTRY")
                    .with_username("USER"),
                ErrorKind::PullImage(PullImageErrorReason::AuthPassword),
            ),
        ];

        for (image_pull_secret, cause) in image_pull_secrets {
            let data = image_pull_secret.data();

            assert_eq!(data.unwrap_err().kind(), &cause);
        }
    }
}
