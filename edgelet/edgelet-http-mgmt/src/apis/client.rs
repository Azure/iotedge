use std::rc::Rc;

use hyper;
use super::configuration::Configuration;

pub struct APIClient<C: hyper::client::Connect> {
    configuration: Rc<Configuration<C>>,
    identity_api: Box<::apis::IdentityApi>,
    module_api: Box<::apis::ModuleApi>,
}

impl<C: hyper::client::Connect> APIClient<C> {
    pub fn new(configuration: Configuration<C>) -> APIClient<C> {
        let rc = Rc::new(configuration);

        APIClient {
            configuration: rc.clone(),
            identity_api: Box::new(::apis::IdentityApiClient::new(rc.clone())),
            module_api: Box::new(::apis::ModuleApiClient::new(rc.clone())),
        }
    }

    pub fn identity_api(&self) -> &::apis::IdentityApi {
        self.identity_api.as_ref()
    }

    pub fn module_api(&self) -> &::apis::ModuleApi {
        self.module_api.as_ref()
    }
}
