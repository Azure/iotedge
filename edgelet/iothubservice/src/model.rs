// Copyright (c) Microsoft. All rights reserved.

use std::default::Default;

use serde_derive::{Deserialize, Serialize};
use serde_json::Value;

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum AuthType {
    None,
    Sas,
    X509,
}

#[derive(Serialize, Deserialize, Debug, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct Twin {
    device_id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    module_id: Option<String>,
    version: i32,
    authentication_type: AuthType,
    properties: Properties,
}

impl Twin {
    pub fn new(
        device_id: &str,
        version: i32,
        authentication_type: AuthType,
        properties: Properties,
    ) -> Self {
        Twin {
            device_id: device_id.to_string(),
            module_id: None,
            version,
            authentication_type,
            properties,
        }
    }

    pub fn with_device_id(mut self, device_id: String) -> Self {
        self.device_id = device_id;
        self
    }

    pub fn with_module_id(mut self, module_id: String) -> Self {
        self.module_id = Some(module_id);
        self
    }

    pub fn with_version(mut self, version: i32) -> Self {
        self.version = version;
        self
    }

    pub fn with_authentication_type(mut self, authentication_type: AuthType) -> Self {
        self.authentication_type = authentication_type;
        self
    }

    pub fn with_properties(mut self, properties: Properties) -> Self {
        self.properties = properties;
        self
    }

    pub fn device_id(&self) -> &str {
        &self.device_id
    }

    pub fn module_id(&self) -> Option<&str> {
        self.module_id.as_ref().map(AsRef::as_ref)
    }

    pub fn version(&self) -> &i32 {
        &self.version
    }

    pub fn authentication_type(&self) -> &AuthType {
        &self.authentication_type
    }

    pub fn properties(&self) -> &Properties {
        &self.properties
    }
}

#[derive(Serialize, Deserialize, Debug, PartialEq)]
pub struct Properties {
    desired: Value,
}

impl Properties {
    pub fn new(desired: Value) -> Properties {
        Properties { desired }
    }

    pub fn desired(&self) -> &Value {
        &self.desired
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct X509Thumbprint {
    #[serde(skip_serializing_if = "Option::is_none")]
    primary_thumbprint: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    secondary_thumbprint: Option<String>,
}

impl X509Thumbprint {
    pub fn new() -> Self {
        X509Thumbprint {
            primary_thumbprint: None,
            secondary_thumbprint: None,
        }
    }

    pub fn with_primary_thumbprint(mut self, primary_thumbprint: String) -> Self {
        self.primary_thumbprint = Some(primary_thumbprint);
        self
    }

    pub fn primary_thumbprint(&self) -> Option<&str> {
        self.primary_thumbprint.as_ref().map(AsRef::as_ref)
    }

    pub fn with_secondary_thumbprint(mut self, secondary_thumbprint: String) -> Self {
        self.secondary_thumbprint = Some(secondary_thumbprint);
        self
    }

    pub fn secondary_thumbprint(&self) -> Option<&str> {
        self.secondary_thumbprint.as_ref().map(AsRef::as_ref)
    }
}

impl Default for X509Thumbprint {
    fn default() -> Self {
        X509Thumbprint::new()
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct SymmetricKey {
    #[serde(skip_serializing_if = "Option::is_none")]
    primary_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    secondary_key: Option<String>,
}

impl SymmetricKey {
    pub fn new() -> Self {
        SymmetricKey {
            primary_key: None,
            secondary_key: None,
        }
    }

    pub fn with_primary_key(mut self, primary_key: String) -> Self {
        self.primary_key = Some(primary_key);
        self
    }

    pub fn primary_key(&self) -> Option<&str> {
        self.primary_key.as_ref().map(AsRef::as_ref)
    }

    pub fn with_secondary_key(mut self, secondary_key: String) -> Self {
        self.secondary_key = Some(secondary_key);
        self
    }

    pub fn secondary_key(&self) -> Option<&str> {
        self.secondary_key.as_ref().map(AsRef::as_ref)
    }
}

impl Default for SymmetricKey {
    fn default() -> Self {
        SymmetricKey::new()
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct AuthMechanism {
    #[serde(skip_serializing_if = "Option::is_none")]
    symmetric_key: Option<SymmetricKey>,

    #[serde(skip_serializing_if = "Option::is_none")]
    x509_thumbprint: Option<X509Thumbprint>,

    #[serde(skip_serializing_if = "Option::is_none")]
    type_: Option<AuthType>,
}

impl AuthMechanism {
    pub fn new() -> Self {
        AuthMechanism {
            symmetric_key: None,
            x509_thumbprint: None,
            type_: None,
        }
    }

    pub fn with_symmetric_key(mut self, symmetric_key: SymmetricKey) -> Self {
        self.symmetric_key = Some(symmetric_key);
        self
    }

    pub fn symmetric_key(&self) -> Option<&SymmetricKey> {
        self.symmetric_key.as_ref()
    }

    pub fn with_x509_thumbprint(mut self, x509_thumbprint: X509Thumbprint) -> Self {
        self.x509_thumbprint = Some(x509_thumbprint);
        self
    }

    pub fn x509_thumbprint(&self) -> Option<&X509Thumbprint> {
        self.x509_thumbprint.as_ref()
    }

    pub fn with_type(mut self, type_: AuthType) -> Self {
        self.type_ = Some(type_);
        self
    }

    pub fn type_(&self) -> Option<AuthType> {
        self.type_
    }
}

impl Default for AuthMechanism {
    fn default() -> Self {
        AuthMechanism::new()
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct Module {
    #[serde(skip_serializing_if = "Option::is_none")]
    module_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    managed_by: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    device_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    generation_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    authentication: Option<AuthMechanism>,
}

impl Module {
    pub fn new() -> Self {
        Module {
            module_id: None,
            managed_by: None,
            device_id: None,
            generation_id: None,
            authentication: None,
        }
    }

    pub fn with_module_id(mut self, module_id: String) -> Self {
        self.module_id = Some(module_id);
        self
    }

    pub fn module_id(&self) -> Option<&str> {
        self.module_id.as_ref().map(AsRef::as_ref)
    }

    pub fn with_managed_by(mut self, managed_by: String) -> Self {
        self.managed_by = Some(managed_by);
        self
    }

    pub fn managed_by(&self) -> Option<&str> {
        self.managed_by.as_ref().map(AsRef::as_ref)
    }

    pub fn with_device_id(mut self, device_id: String) -> Self {
        self.device_id = Some(device_id);
        self
    }

    pub fn device_id(&self) -> Option<&str> {
        self.device_id.as_ref().map(AsRef::as_ref)
    }

    pub fn with_generation_id(mut self, generation_id: String) -> Self {
        self.generation_id = Some(generation_id);
        self
    }

    pub fn generation_id(&self) -> Option<&str> {
        self.generation_id.as_ref().map(AsRef::as_ref)
    }

    pub fn with_authentication(mut self, authentication: AuthMechanism) -> Self {
        self.authentication = Some(authentication);
        self
    }

    pub fn authentication(&self) -> Option<&AuthMechanism> {
        self.authentication.as_ref()
    }
}

impl Default for Module {
    fn default() -> Self {
        Module::new()
    }
}
