use std::rc::Rc;

use hyper;
use super::configuration::Configuration;

pub struct APIClient<C: hyper::client::Connect> {
  configuration: Rc<Configuration<C>>,
  test_operation_result_api: Box<::apis::TestOperationResultApi>,
}

impl<C: hyper::client::Connect> APIClient<C> {
  pub fn new(configuration: Configuration<C>) -> APIClient<C> {
    let rc = Rc::new(configuration);

    APIClient {
      configuration: rc.clone(),
      test_operation_result_api: Box::new(::apis::TestOperationResultApiClient::new(rc.clone())),
    }
  }

  pub fn test_operation_result_api(&self) -> &::apis::TestOperationResultApi{
    self.test_operation_result_api.as_ref()
  }


}
