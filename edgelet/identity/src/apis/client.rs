use std::sync::Arc;

use super::configuration::Configuration;

pub struct APIClient {
  identity_operations_api: Box<dyn crate::apis::IdentityOperationsApi>,
}

impl APIClient {
  pub fn new<C>(configuration: Configuration<C>) -> Self
    where
        C: hyper::client::connect::Connect + 'static,
  {
    let configuration = Arc::new(configuration);

    APIClient {
      identity_operations_api: Box::new(crate::apis::IdentityOperationsApiClient::new(configuration)),
    }
  }

  pub fn identity_operations_api(&self) -> &dyn crate::apis::IdentityOperationsApi{
    self.identity_operations_api.as_ref()
  }


}
