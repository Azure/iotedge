/*
 * IoT Edge External Provisioning Environment API
 *
 * No description provided (generated by Swagger Codegen https://github.com/swagger-api/swagger-codegen)
 *
 * OpenAPI spec version: 2019-04-10
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct Credentials {
    /// Indicates the type of authentication credential used.
    #[serde(rename = "authType")]
    auth_type: String,
    /// Indicates the source of the authentication credential.
    #[serde(rename = "source")]
    source: String,
    /// The symmetric key used for authentication. Specified only if the 'authType' is 'symmetric-key' and the 'source' is 'payload'.
    #[serde(rename = "key", skip_serializing_if = "Option::is_none")]
    key: Option<String>,
    /// The identity certificate. Should be a PEM formatted byte array if the 'authType' is 'x509' and the 'source' is 'payload' or should be a reference to the certificate if the 'authType' is 'x509' and the 'source' is 'hsm'.
    #[serde(rename = "identityCert", skip_serializing_if = "Option::is_none")]
    identity_cert: Option<String>,
    /// The identity private key. Should be a PEM formatted byte array if the 'authType' is 'x509' and the 'source' is 'payload' or should be a reference to the private key if the 'authType' is 'x509' and the 'source' is 'hsm'.
    #[serde(rename = "identityPrivateKey", skip_serializing_if = "Option::is_none")]
    identity_private_key: Option<String>,
}

impl Credentials {
    pub fn new(auth_type: String, source: String) -> Credentials {
        Credentials {
            auth_type,
            source,
            key: None,
            identity_cert: None,
            identity_private_key: None,
        }
    }

    pub fn set_auth_type(&mut self, auth_type: String) {
        self.auth_type = auth_type;
    }

    pub fn with_auth_type(mut self, auth_type: String) -> Credentials {
        self.auth_type = auth_type;
        self
    }

    pub fn auth_type(&self) -> &str {
        &self.auth_type
    }

    pub fn set_source(&mut self, source: String) {
        self.source = source;
    }

    pub fn with_source(mut self, source: String) -> Credentials {
        self.source = source;
        self
    }

    pub fn source(&self) -> &str {
        &self.source
    }

    pub fn set_key(&mut self, key: String) {
        self.key = Some(key);
    }

    pub fn with_key(mut self, key: String) -> Credentials {
        self.key = Some(key);
        self
    }

    pub fn key(&self) -> Option<&str> {
        self.key.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_key(&mut self) {
        self.key = None;
    }

    pub fn set_identity_cert(&mut self, identity_cert: String) {
        self.identity_cert = Some(identity_cert);
    }

    pub fn with_identity_cert(mut self, identity_cert: String) -> Credentials {
        self.identity_cert = Some(identity_cert);
        self
    }

    pub fn identity_cert(&self) -> Option<&str> {
        self.identity_cert.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_identity_cert(&mut self) {
        self.identity_cert = None;
    }

    pub fn set_identity_private_key(&mut self, identity_private_key: String) {
        self.identity_private_key = Some(identity_private_key);
    }

    pub fn with_identity_private_key(mut self, identity_private_key: String) -> Credentials {
        self.identity_private_key = Some(identity_private_key);
        self
    }

    pub fn identity_private_key(&self) -> Option<&str> {
        self.identity_private_key.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_identity_private_key(&mut self) {
        self.identity_private_key = None;
    }
}
