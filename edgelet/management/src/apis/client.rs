use std::rc::Rc;

use super::configuration::Configuration;
use hyper;

pub struct APIClient<C: hyper::client::Connect> {
    configuration: Rc<Configuration<C>>,
    identity_api: Box<::apis::IdentityApi>,
    module_api: Box<::apis::ModuleApi>,
    system_information_api: Box<::apis::SystemInformationApi>,
}

impl<C: hyper::client::Connect> APIClient<C> {
    pub fn new(configuration: Configuration<C>) -> APIClient<C> {
        let rc = Rc::new(configuration);

        APIClient {
            configuration: rc.clone(),
            identity_api: Box::new(::apis::IdentityApiClient::new(rc.clone())),
            module_api: Box::new(::apis::ModuleApiClient::new(rc.clone())),
            system_information_api: Box::new(::apis::SystemInformationApiClient::new(rc.clone())),
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
