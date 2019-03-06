// Copyright (c) Microsoft. All rights reserved.

use std::sync::Mutex;

use failure::{Fail, ResultExt};
use futures::{Future, IntoFuture};
use hyper::{Body, Request, Response, StatusCode};

use edgelet_core::{IdentityManager, IdentityOperation, IdentitySpec};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct DeleteIdentity<I> {
    id_manager: Mutex<I>,
}

impl<I> DeleteIdentity<I> {
    pub fn new(id_manager: I) -> Self {
        DeleteIdentity {
            id_manager: Mutex::new(id_manager),
        }
    }
}

impl<I> Handler<Parameters> for DeleteIdentity<I>
where
    I: 'static + IdentityManager + Send,
{
    fn handle(
        &self,
        _req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .map(|name| {
                let name = name.to_string();

                self.id_manager
                    .lock()
                    .unwrap()
                    .delete(IdentitySpec::new(name.clone()))
                    .then(|result| match result {
                        Ok(_) => Ok(name),
                        Err(err) => Err(Error::from(err.context(ErrorKind::IdentityOperation(
                            IdentityOperation::DeleteIdentity(name),
                        )))),
                    })
            })
            .into_future()
            .flatten()
            .and_then(|name| {
                Ok(Response::builder()
                    .status(StatusCode::NO_CONTENT)
                    .body(Body::default())
                    .context(ErrorKind::IdentityOperation(
                        IdentityOperation::DeleteIdentity(name),
                    ))?)
            })
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_core::{AuthType, Identity};
    use edgelet_test_utils::identity::{TestIdentity, TestIdentityManager};
    use futures::Stream;
    use management::models::ErrorResponse;
    use serde_json;

    use super::*;

    #[test]
    fn delete_succeeds() {
        let manager = TestIdentityManager::new(vec![
            TestIdentity::new("m1", "iotedge", "1", AuthType::Sas),
            TestIdentity::new("m2", "iotedge", "2", AuthType::Sas),
            TestIdentity::new("m3", "iotedge", "3", AuthType::Sas),
        ]);
        let handler = DeleteIdentity::new(manager);
        let request = Request::delete("http://localhost/identities")
            .body(Body::default())
            .unwrap();
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "m2".to_string())]);

        let response = handler.handle(request, parameters).wait().unwrap();
        assert_eq!(StatusCode::NO_CONTENT, response.status());

        let list = handler.id_manager.lock().unwrap().list().wait().unwrap();
        assert_eq!(2, list.len());
        assert_eq!(
            None,
            list.iter().position(|ref mid| mid.module_id() == "m2")
        );
    }

    #[test]
    fn delete_no_name_param() {
        let manager = TestIdentityManager::new(vec![]);
        let handler = DeleteIdentity::new(manager);
        let request = Request::delete("http://localhost/identities")
            .body(Body::default())
            .unwrap();
        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();
        response
            .into_body()
            .concat2()
            .and_then(|body| {
                let error: ErrorResponse = serde_json::from_slice(&body).unwrap();
                assert_eq!(
                    "The request is missing required parameter `name`",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn delete_fails() {
        let manager = TestIdentityManager::new(vec![]).with_fail_create(true);
        let handler = DeleteIdentity::new(manager);
        let request = Request::delete("http://localhost/identities")
            .body(Body::default())
            .unwrap();
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "m1".to_string())]);

        let response = handler.handle(request, parameters).wait().unwrap();
        response
            .into_body()
            .concat2()
            .and_then(|body| {
                let error: ErrorResponse = serde_json::from_slice(&body).unwrap();
                assert_eq!(
                    "Could not delete identity m1\n\tcaused by: Module not found",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
