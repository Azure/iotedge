use std::sync::Arc;

use super::configuration::Configuration;
use hyper;

pub struct APIClient {
    external_provisioning_api: Box<dyn crate::apis::ExternalProvisioningApi>,
}

impl APIClient {
    pub fn new<C>(configuration: Configuration<C>) -> Self
    where
        C: hyper::client::connect::Connect + 'static,
    {
        let configuration = Arc::new(configuration);

        APIClient {
            external_provisioning_api: Box::new(crate::apis::ExternalProvisioningApiClient::new(
                configuration,
            )),
        }
    }

    pub fn external_provisioning_api(&self) -> &dyn crate::apis::ExternalProvisioningApi {
        self.external_provisioning_api.as_ref()
    }
}
