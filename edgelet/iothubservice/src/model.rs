// Copyright (c) Microsoft. All rights reserved.

use std::default::Default;

use serde_json::Value;

#[derive(Serialize, Deserialize, Debug, PartialEq)]
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
    ) -> Twin {
        Twin {
            device_id: device_id.to_string(),
            module_id: None,
            version,
            authentication_type,
            properties,
        }
    }

    pub fn with_device_id(mut self, device_id: String) -> Twin {
        self.device_id = device_id;
        self
    }

    pub fn with_module_id(mut self, module_id: String) -> Twin {
        self.module_id = Some(module_id);
        self
    }

    pub fn with_version(mut self, version: i32) -> Twin {
        self.version = version;
        self
    }

    pub fn with_authentication_type(mut self, authentication_type: AuthType) -> Twin {
        self.authentication_type = authentication_type;
        self
    }

    pub fn with_properties(mut self, properties: Properties) -> Twin {
        self.properties = properties;
        self
    }

    pub fn device_id(&self) -> &str {
        &self.device_id
    }

    pub fn module_id(&self) -> Option<&String> {
        self.module_id.as_ref()
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

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct X509Thumbprint {
    #[serde(skip_serializing_if = "Option::is_none")]
    primary_thumbprint: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    secondary_thumbprint: Option<String>,
}

impl X509Thumbprint {
    pub fn new() -> X509Thumbprint {
        X509Thumbprint {
            primary_thumbprint: None,
            secondary_thumbprint: None,
        }
    }

    pub fn with_primary_thumbprint(mut self, primary_thumbprint: String) -> X509Thumbprint {
        self.primary_thumbprint = Some(primary_thumbprint);
        self
    }

    pub fn primary_thumbprint(&self) -> Option<&String> {
        self.primary_thumbprint.as_ref()
    }

    pub fn with_secondary_thumbprint(mut self, secondary_thumbprint: String) -> X509Thumbprint {
        self.secondary_thumbprint = Some(secondary_thumbprint);
        self
    }

    pub fn secondary_thumbprint(&self) -> Option<&String> {
        self.secondary_thumbprint.as_ref()
    }
}

impl Default for X509Thumbprint {
    fn default() -> Self {
        X509Thumbprint::new()
    }
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SymmetricKey {
    #[serde(skip_serializing_if = "Option::is_none")]
    primary_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    secondary_key: Option<String>,
}

impl SymmetricKey {
    pub fn new() -> SymmetricKey {
        SymmetricKey {
            primary_key: None,
            secondary_key: None,
        }
    }

    pub fn with_primary_key(mut self, primary_key: String) -> SymmetricKey {
        self.primary_key = Some(primary_key);
        self
    }

    pub fn primary_key(&self) -> Option<&String> {
        self.primary_key.as_ref()
    }

    pub fn with_secondary_key(mut self, secondary_key: String) -> SymmetricKey {
        self.secondary_key = Some(secondary_key);
        self
    }

    pub fn secondary_key(&self) -> Option<&String> {
        self.secondary_key.as_ref()
    }
}

impl Default for SymmetricKey {
    fn default() -> Self {
        SymmetricKey::new()
    }
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AuthMechanism {
    #[serde(skip_serializing_if = "Option::is_none")]
    symmetric_key: Option<SymmetricKey>,
    #[serde(skip_serializing_if = "Option::is_none")]
    x509_thumbprint: Option<X509Thumbprint>,
    #[serde(skip_serializing_if = "Option::is_none")]
    _type: Option<AuthType>,
}

impl AuthMechanism {
    pub fn new() -> AuthMechanism {
        AuthMechanism {
            symmetric_key: None,
            x509_thumbprint: None,
            _type: None,
        }
    }

    pub fn with_symmetric_key(mut self, symmetric_key: SymmetricKey) -> AuthMechanism {
        self.symmetric_key = Some(symmetric_key);
        self
    }

    pub fn symmetric_key(&self) -> Option<&SymmetricKey> {
        self.symmetric_key.as_ref()
    }

    pub fn with_x509_thumbprint(mut self, x509_thumbprint: X509Thumbprint) -> AuthMechanism {
        self.x509_thumbprint = Some(x509_thumbprint);
        self
    }

    pub fn x509_thumbprint(&self) -> Option<&X509Thumbprint> {
        self.x509_thumbprint.as_ref()
    }

    pub fn with_type(mut self, _type: AuthType) -> AuthMechanism {
        self._type = Some(_type);
        self
    }

    pub fn _type(&self) -> Option<&AuthType> {
        self._type.as_ref()
    }
}

impl Default for AuthMechanism {
    fn default() -> Self {
        AuthMechanism::new()
    }
}

#[derive(Debug, Serialize, Deserialize)]
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
    pub fn new() -> Module {
        Module {
            module_id: None,
            managed_by: None,
            device_id: None,
            generation_id: None,
            authentication: None,
        }
    }

    pub fn with_module_id(mut self, module_id: String) -> Module {
        self.module_id = Some(module_id);
        self
    }

    pub fn module_id(&self) -> Option<&String> {
        self.module_id.as_ref()
    }

    pub fn with_managed_by(mut self, managed_by: String) -> Module {
        self.managed_by = Some(managed_by);
        self
    }

    pub fn managed_by(&self) -> Option<&String> {
        self.managed_by.as_ref()
    }

    pub fn with_device_id(mut self, device_id: String) -> Module {
        self.device_id = Some(device_id);
        self
    }

    pub fn device_id(&self) -> Option<&String> {
        self.device_id.as_ref()
    }

    pub fn with_generation_id(mut self, generation_id: String) -> Module {
        self.generation_id = Some(generation_id);
        self
    }

    pub fn generation_id(&self) -> Option<&String> {
        self.generation_id.as_ref()
    }

    pub fn with_authentication(mut self, authentication: AuthMechanism) -> Module {
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
