// Copyright (c) Microsoft. All rights reserved.

use aziot_identity_common::Identity;

pub struct IdentityClient {}

impl IdentityClient {
    pub async fn create_module_identity(
        &self,
        module_name: &str,
    ) -> Result<Identity, std::io::Error> {
        todo!()
    }

    pub async fn get_identities(&self) -> Result<Vec<Identity>, std::io::Error> {
        todo!()
    }

    pub async fn update_module_identity(
        &self,
        module_name: &str,
    ) -> Result<Identity, std::io::Error> {
        todo!()
    }

    pub async fn delete_identity(&self, module_name: &str) -> Result<(), std::io::Error> {
        todo!()
    }
}
