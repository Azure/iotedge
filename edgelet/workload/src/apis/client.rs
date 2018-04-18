use std::rc::Rc;

use hyper;
use super::configuration::Configuration;

pub struct APIClient<C: hyper::client::Connect> {
    configuration: Rc<Configuration<C>>,
    workload_api: Box<::apis::WorkloadApi>,
}

impl<C: hyper::client::Connect> APIClient<C> {
    pub fn new(configuration: Configuration<C>) -> APIClient<C> {
        let rc = Rc::new(configuration);

        APIClient {
            configuration: rc.clone(),
            workload_api: Box::new(::apis::WorkloadApiClient::new(rc.clone())),
        }
    }

    pub fn workload_api(&self) -> &::apis::WorkloadApi {
        self.workload_api.as_ref()
    }
}
