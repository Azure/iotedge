// Copyright (c) Microsoft. All rights reserved.

use std::rc::Rc;

use hyper::client::{Client as HyperClient, Connect};

use device::DeviceClient;
use error::Result;
use module::ModuleClient;

pub struct Client<C: Connect> {
    client: Rc<HyperClient<C>>,
    api_version: String,
    user_agent: Option<String>,
    sas_token: Option<String>,
    host_name: Option<String>,
}

impl<C: Connect> Client<C> {
    pub fn new(client: HyperClient<C>, api_version: &str) -> Result<Client<C>> {
        Ok(Client {
            client: Rc::new(client),
            api_version: ensure_not_empty!(api_version).to_string(),
            user_agent: None,
            sas_token: None,
            host_name: None,
        })
    }

    pub fn with_user_agent(mut self, user_agent: &str) -> Client<C> {
        self.user_agent = Some(user_agent.to_string());
        self
    }

    pub fn with_sas_token(mut self, sas_token: &str) -> Client<C> {
        self.sas_token = Some(sas_token.to_string());
        self
    }

    pub fn with_host_name(mut self, host_name: &str) -> Client<C> {
        self.host_name = Some(host_name.to_string());
        self
    }

    pub fn client(&self) -> &HyperClient<C> {
        self.client.as_ref()
    }

    pub fn api_version(&self) -> &str {
        &self.api_version
    }

    pub fn user_agent(&self) -> Option<&String> {
        self.user_agent.as_ref()
    }

    pub fn sas_token(&self) -> Option<&String> {
        self.sas_token.as_ref()
    }

    pub fn host_name(&self) -> Option<&String> {
        self.host_name.as_ref()
    }

    pub fn create_device_client(&self, device_id: &str) -> Result<DeviceClient<C>> {
        DeviceClient::new(self.clone(), device_id)
    }

    pub fn create_module_client(
        &self,
        device_id: &str,
        module_id: &str,
    ) -> Result<ModuleClient<C>> {
        ModuleClient::new(self.clone(), device_id, module_id)
    }
}

impl<C: Connect> Clone for Client<C> {
    fn clone(&self) -> Self {
        Client {
            client: self.client.clone(),
            api_version: self.api_version.clone(),
            user_agent: self.user_agent.as_ref().cloned(),
            sas_token: self.sas_token.as_ref().cloned(),
            host_name: self.host_name.as_ref().cloned(),
        }
    }
}
