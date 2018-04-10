// Copyright (c) Microsoft. All rights reserved.

use hyper::client::Connect;

use client::Client;
use error::Result;
use module::ModuleClient;

pub struct DeviceClient<C: Connect> {
    client: Client<C>,
    device_id: String,
}

impl<C: Connect> DeviceClient<C> {
    pub fn new(client: Client<C>, device_id: &str) -> Result<DeviceClient<C>> {
        Ok(DeviceClient {
            client,
            device_id: ensure_not_empty!(device_id).to_string(),
        })
    }

    pub fn create_module_client(&self, module_id: &str) -> Result<ModuleClient<C>> {
        ModuleClient::new(self.client.clone(), &self.device_id, module_id)
    }

    pub fn device_id(&self) -> &str {
        self.device_id.as_ref()
    }
}
