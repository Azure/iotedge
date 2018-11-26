use std::sync::Arc;

use super::configuration::Configuration;
use hyper;

pub struct APIClient {
    identity_api: Box<::apis::IdentityApi>,
    module_api: Box<::apis::ModuleApi>,
    system_information_api: Box<::apis::SystemInformationApi>,
}

impl APIClient {
    pub fn new<C>(configuration: Configuration<C>) -> Self
    where
        C: hyper::client::connect::Connect + 'static,
    {
        let configuration = Arc::new(configuration);

        APIClient {
            identity_api: Box::new(::apis::IdentityApiClient::new(configuration.clone())),
            module_api: Box::new(::apis::ModuleApiClient::new(configuration.clone())),
            system_information_api: Box::new(::apis::SystemInformationApiClient::new(
                configuration.clone(),
            )),
        }
    }

    pub fn identity_api(&self) -> &::apis::IdentityApi {
        self.identity_api.as_ref()
    }

    pub fn module_api(&self) -> &::apis::ModuleApi {
        self.module_api.as_ref()
    }

    pub fn system_information_api(&self) -> &::apis::SystemInformationApi {
        self.system_information_api.as_ref()
    }
}
