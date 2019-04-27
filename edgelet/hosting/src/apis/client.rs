use std::sync::Arc;

use super::configuration::Configuration;
use hyper;

pub struct APIClient {
    hosting_api: Box<dyn crate::apis::HostingApi>,
}

impl APIClient {
    pub fn new<C>(configuration: Configuration<C>) -> Self
    where
        C: hyper::client::connect::Connect + 'static,
    {
        let configuration = Arc::new(configuration);

        APIClient {
            hosting_api: Box::new(crate::apis::HostingApiClient::new(configuration.clone())),
        }
    }

    pub fn hosting_api(&self) -> &dyn crate::apis::HostingApi {
        self.hosting_api.as_ref()
    }
}
