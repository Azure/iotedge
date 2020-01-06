use std::sync::Arc;

use super::configuration::Configuration;
use hyper;

pub struct APIClient {
    workload_api: Box<dyn (crate::apis::WorkloadApi)>,
}

impl APIClient {
    pub fn new<C>(configuration: Configuration<C>) -> Self
    where
        C: hyper::client::connect::Connect + 'static,
    {
        let configuration = Arc::new(configuration);

        APIClient {
            workload_api: Box::new(crate::apis::WorkloadApiClient::new(configuration)),
        }
    }

    pub fn workload_api(&self) -> &dyn (crate::apis::WorkloadApi) {
        self.workload_api.as_ref()
    }
}
