use std::sync::Arc;

use hyper;
use super::configuration::Configuration;

pub struct APIClient {
  device_actions_api: Box<dyn crate::apis::DeviceActionsApi>,
  identity_api: Box<dyn crate::apis::IdentityApi>,
  module_api: Box<dyn crate::apis::ModuleApi>,
  secret_api: Box<dyn crate::apis::SecretApi>,
  system_information_api: Box<dyn crate::apis::SystemInformationApi>,
}

impl APIClient {
  pub fn new<C: hyper::client::connect::Connect + 'static>(configuration: Configuration<C>) -> APIClient {
    let arc = Arc::new(configuration);

    APIClient {
      device_actions_api: Box::new(crate::apis::DeviceActionsApiClient::new(arc.clone())),
      identity_api: Box::new(crate::apis::IdentityApiClient::new(arc.clone())),
      module_api: Box::new(crate::apis::ModuleApiClient::new(arc.clone())),
      secret_api: Box::new(crate::apis::SecretApiClient::new(arc.clone())),
      system_information_api: Box::new(crate::apis::SystemInformationApiClient::new(arc.clone())),
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

  pub fn secret_api(&self) -> &dyn crate::apis::SecretApi {
    self.secret_api.as_ref()
  }

  pub fn system_information_api(&self) -> &dyn crate::apis::SystemInformationApi {
    self.system_information_api.as_ref()
  }


}
