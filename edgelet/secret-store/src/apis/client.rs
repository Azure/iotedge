use std::sync::Arc;

use hyper;
use super::configuration::Configuration;

pub struct APIClient {
  default_api: Box<::apis::DefaultApi>,
}

impl APIClient {
  pub fn new<C: hyper::client::connect::Connect + 'static>(configuration: Configuration<C>) -> APIClient {
    let arc = Arc::new(configuration);

    APIClient {
      default_api: Box::new(::apis::DefaultApiClient::new(arc.clone())),
    }
  }

  pub fn default_api(&self) -> &::apis::DefaultApi{
    self.default_api.as_ref()
  }


}
