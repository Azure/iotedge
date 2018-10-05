use std::sync::Arc;

use super::configuration::Configuration;
use hyper;

pub struct APIClient<C: hyper::client::connect::Connect> {
    configuration: Arc<Configuration<C>>,
    workload_api: Box<::apis::WorkloadApi>,
}

impl<C: hyper::client::connect::Connect + 'static> APIClient<C> {
    pub fn new(configuration: Configuration<C>) -> APIClient<C> {
        let configuration = Arc::new(configuration);

        APIClient {
            configuration: configuration.clone(),
            workload_api: Box::new(::apis::WorkloadApiClient::new(configuration.clone())),
        }
    }

    pub fn workload_api(&self) -> &::apis::WorkloadApi {
        self.workload_api.as_ref()
    }
}
