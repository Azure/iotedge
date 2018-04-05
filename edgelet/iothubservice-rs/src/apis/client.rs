use std::rc::Rc;

use hyper;
use super::configuration::Configuration;

pub struct APIClient<C: hyper::client::Connect> {
    configuration: Rc<Configuration<C>>,
    device_api: Box<::apis::DeviceApi>,
    device_twin_api: Box<::apis::DeviceTwinApi>,
    module_api: Box<::apis::ModuleApi>,
}

impl<C: hyper::client::Connect> APIClient<C> {
    pub fn new(configuration: Configuration<C>) -> APIClient<C> {
        let rc = Rc::new(configuration);

        APIClient {
            configuration: rc.clone(),
            device_api: Box::new(::apis::DeviceApiClient::new(rc.clone())),
            device_twin_api: Box::new(::apis::DeviceTwinApiClient::new(rc.clone())),
            module_api: Box::new(::apis::ModuleApiClient::new(rc.clone())),
        }
    }

    pub fn device_api(&self) -> &::apis::DeviceApi {
        self.device_api.as_ref()
    }

    pub fn device_twin_api(&self) -> &::apis::DeviceTwinApi {
        self.device_twin_api.as_ref()
    }

    pub fn module_api(&self) -> &::apis::ModuleApi {
        self.module_api.as_ref()
    }
}
