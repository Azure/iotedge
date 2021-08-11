// Copyright (c) Microsoft. All rights reserved.

use aziot_identity_common::Identity;

pub struct IdentityClient {
    pub create_identity_ok: bool,
    pub get_identities_ok: bool,
    pub get_identity_ok: bool,
    pub update_identity_ok: bool,
    pub delete_identity_ok: bool,
}

impl Default for IdentityClient {
    fn default() -> Self {
        IdentityClient {
            create_identity_ok: true,
            get_identities_ok: true,
            get_identity_ok: true,
            update_identity_ok: true,
            delete_identity_ok: true,
        }
    }
}

impl IdentityClient {
    pub async fn create_module_identity(
        &self,
        module_name: &str,
    ) -> Result<Identity, std::io::Error> {
        if self.get_identity_ok {
            Ok(test_identity(module_name))
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn get_identities(&self) -> Result<Vec<Identity>, std::io::Error> {
        if self.get_identities_ok {
            let identities = vec![
                test_identity("testModule1"),
                test_identity("testModule2"),
                test_identity("testModule3"),
            ];

            Ok(identities)
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn get_identity(&self, module_name: &str) -> Result<Identity, std::io::Error> {
        if self.get_identity_ok {
            Ok(test_identity(module_name))
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn update_module_identity(
        &self,
        module_name: &str,
    ) -> Result<Identity, std::io::Error> {
        if self.update_identity_ok {
            Ok(test_identity(module_name))
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn delete_identity(&self, _module_name: &str) -> Result<(), std::io::Error> {
        if self.delete_identity_ok {
            Ok(())
        } else {
            Err(crate::test_error())
        }
    }
}

/// Generates an Identity struct for a given module name.
fn test_identity(module_name: &str) -> Identity {
    Identity::Aziot(aziot_identity_common::AzureIoTSpec {
        hub_name: "test-hub.test.net".to_string(),
        gateway_host: "gateway-host.test.net".to_string(),
        device_id: aziot_identity_common::DeviceId("test-device".to_string()),
        module_id: Some(aziot_identity_common::ModuleId(module_name.to_string())),
        gen_id: Some(aziot_identity_common::GenId("test-gen-id".to_string())),
        auth: Some(aziot_identity_common::AuthenticationInfo {
            auth_type: aziot_identity_common::AuthenticationType::X509,
            key_handle: Some(aziot_key_common::KeyHandle(format!("{}-key", module_name))),
            cert_id: Some(format!("{}-cert", module_name)),
        }),
    })
}
