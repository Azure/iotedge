// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Identity as CoreIdentity, IdentityManager};
use edgelet_http::route::{Handler, Parameters};
use failure::ResultExt;
use futures::{future, Future};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use management::models::{Identity, IdentityList};
use serde::Serialize;
use serde_json;

use error::ErrorKind;
use IntoResponse;

pub struct ListIdentities<I>
where
    I: 'static + IdentityManager,
    I::Identity: Serialize,
{
    id_manager: I,
}

impl<I> ListIdentities<I>
where
    I: 'static + IdentityManager,
    I::Identity: Serialize,
{
    pub fn new(id_manager: I) -> Self {
        ListIdentities { id_manager }
    }
}

impl<I> Handler<Parameters> for ListIdentities<I>
where
    I: 'static + IdentityManager + Send,
    I::Identity: Serialize,
{
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
        let response = self.id_manager.list().then(|result| {
            result
                .context(ErrorKind::IdentityManager)
                .map(|identities| {
                    let body = IdentityList::new(
                        identities
                            .iter()
                            .map(|identity| {
                                Identity::new(
                                    identity.module_id().to_string(),
                                    identity.managed_by().to_string(),
                                    identity.generation_id().to_string(),
                                    identity.auth_type().to_string(),
                                )
                            }).collect(),
                    );
                    let result = serde_json::to_string(&body)
                        .context(ErrorKind::Serde)
                        .map(|b| {
                            Response::builder()
                                .status(StatusCode::OK)
                                .header(CONTENT_TYPE, "application/json")
                                .header(CONTENT_LENGTH, b.len().to_string().as_str())
                                .body(b.into())
                                .unwrap_or_else(|e| e.into_response())
                        }).unwrap_or_else(|e| e.into_response());

                    future::ok(result)
                }).unwrap_or_else(|e| future::ok(e.into_response()))
        });

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_core::AuthType;
    use edgelet_test_utils::identity::{TestIdentity, TestIdentityManager};
    use futures::Stream;
    use management::models::ErrorResponse;

    use super::*;

    #[test]
    fn list_succeeds() {
        let manager = TestIdentityManager::new(vec![
            TestIdentity::new("m1", "iotedge", "1", AuthType::Sas),
            TestIdentity::new("m2", "iotedge", "2", AuthType::Sas),
            TestIdentity::new("m3", "iotedge", "3", AuthType::Sas),
        ]);
        let handler = ListIdentities::new(manager);
        let request = Request::get("http://localhost/identities")
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
                let list: IdentityList = serde_json::from_slice(&body).unwrap();
                for i in 1..4 {
                    assert_eq!(&format!("m{}", i), list.identities()[i - 1].module_id());
                    assert_eq!("iotedge", list.identities()[i - 1].managed_by());
                    assert_eq!(&format!("{}", i), list.identities()[i - 1].generation_id());
                }

                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn list_fails() {
        let manager = TestIdentityManager::new(vec![]).with_fail_list(true);
        let handler = ListIdentities::new(manager);
        let request = Request::get("http://localhost/identities")
            .body(Body::default())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        response
            .into_body()
            .concat2()
            .and_then(|body| {
                let error: ErrorResponse = serde_json::from_slice(&body).unwrap();
                assert_eq!(
                    "Identity manager error\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            }).wait()
            .unwrap();
    }
}
