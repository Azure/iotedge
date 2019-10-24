use std::sync::Arc;

use super::configuration::Configuration;
use hyper;

pub struct APIClient {
    device_actions_api: Box<dyn crate::apis::DeviceActionsApi>,
    identity_api: Box<dyn crate::apis::IdentityApi>,
    module_api: Box<dyn crate::apis::ModuleApi>,
    system_information_api: Box<dyn crate::apis::SystemInformationApi>,
}

impl APIClient {
    pub fn new<C>(configuration: Configuration<C>) -> Self
    where
        C: hyper::client::connect::Connect + 'static,
    {
        let configuration = Arc::new(configuration);

        APIClient {
            device_actions_api: Box::new(crate::apis::DeviceActionsApiClient::new(
                configuration.clone(),
            )),
            identity_api: Box::new(crate::apis::IdentityApiClient::new(configuration.clone())),
            module_api: Box::new(crate::apis::ModuleApiClient::new(configuration.clone())),
            system_information_api: Box::new(crate::apis::SystemInformationApiClient::new(
                configuration.clone(),
            )),
        }
    }

    pub fn device_actions_api(&self) -> &dyn crate::apis::DeviceActionsApi {
        self.device_actions_api.as_ref()
    }

    pub fn identity_api(&self) -> &dyn crate::apis::IdentityApi {
        self.identity_api.as_ref()
    }

    pub fn module_api(&self) -> &dyn crate::apis::ModuleApi {
        self.module_api.as_ref()
    }

    pub fn system_information_api(&self) -> &dyn crate::apis::SystemInformationApi {
        self.system_information_api.as_ref()
    }
}
