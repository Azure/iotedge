// Copyright (c) Microsoft. All rights reserved.

use aziot_identity_common::Identity;

pub struct IdentityClient {
    pub create_identity_ok: bool,
    pub get_identities_ok: bool,
    pub get_identity_ok: bool,
    pub update_identity_ok: bool,
    pub delete_identity_ok: bool,

    pub identities: std::collections::BTreeMap<String, Identity>,
}

impl Default for IdentityClient {
    fn default() -> Self {
        let mut identities = std::collections::BTreeMap::new();
        identities.insert("testModule".to_string(), test_identity("testModule"));

        IdentityClient {
            create_identity_ok: true,
            get_identities_ok: true,
            get_identity_ok: true,
            update_identity_ok: true,
            delete_identity_ok: true,

            identities,
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
            let mut identities = vec![];

            for (_, identity) in &self.identities {
                identities.push(identity.clone())
            }

            Ok(identities)
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn get_identity(&self, module_name: &str) -> Result<Identity, std::io::Error> {
        if self.get_identity_ok {
            match self.identities.get(module_name) {
                Some(identity) => Ok(identity.clone()),
                None => Err(crate::test_error()),
            }
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn update_module_identity(
        &self,
        module_name: &str,
    ) -> Result<Identity, std::io::Error> {
        if self.update_identity_ok {
            // A real identity client would update the Idenitity in Hub. But this test
            // client can just return the existing Identity.
            self.get_identity(module_name).await
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn delete_identity(&self, module_name: &str) -> Result<(), std::io::Error> {
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
