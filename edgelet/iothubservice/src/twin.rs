// Copyright (c) Microsoft. All rights reserved.

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
