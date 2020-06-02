use std::sync::Arc;

use super::configuration::Configuration;

pub struct APIClient {
  key_operations_api: Box<dyn crate::apis::KeyOperationsApi>,
}

impl APIClient {
  pub fn new<C>(configuration: Configuration<C>) -> Self
    where
        C: hyper::client::connect::Connect + 'static,
  {
    let configuration = Arc::new(configuration);

    APIClient {
      key_operations_api: Box::new(crate::apis::KeyOperationsApiClient::new(configuration)),
    }
  }

  pub fn key_operations_api(&self) -> &dyn crate::apis::KeyOperationsApi {
    self.key_operations_api.as_ref()
  }
}