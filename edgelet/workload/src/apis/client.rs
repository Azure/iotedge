use std::sync::Arc;

use hyper;
use super::configuration::Configuration;

pub struct APIClient {
  module_api: Box<dyn crate::apis::ModuleApi>,
  secret_api: Box<dyn crate::apis::SecretApi>,
  workload_api: Box<dyn crate::apis::WorkloadApi>,
}

impl APIClient {
  pub fn new<C: hyper::client::connect::Connect + 'static>(configuration: Configuration<C>) -> APIClient {
    let arc = Arc::new(configuration);

    APIClient {
      module_api: Box::new(crate::apis::ModuleApiClient::new(arc.clone())),
      secret_api: Box::new(crate::apis::SecretApiClient::new(arc.clone())),
      workload_api: Box::new(crate::apis::WorkloadApiClient::new(arc.clone())),
    }
  }

  pub fn module_api(&self) -> &dyn crate::apis::ModuleApi {
    self.module_api.as_ref()
  }

  pub fn secret_api(&self) -> &dyn crate::apis::SecretApi {
    self.secret_api.as_ref()
  }

  pub fn workload_api(&self) -> &dyn crate::apis::WorkloadApi {
    self.workload_api.as_ref()
  }


}
