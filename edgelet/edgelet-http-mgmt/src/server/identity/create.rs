// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;
use std::sync::Arc;

use failure::ResultExt;
use futures::future::{self, FutureResult};
use futures::{Future, Stream};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use serde::Serialize;
use serde_json;

use edgelet_core::{Identity as CoreIdentity, IdentityManager, IdentitySpec};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use management::models::{Identity, IdentitySpec as CreateIdentitySpec};

use error::{Error, ErrorKind};
use IntoResponse;

pub struct CreateIdentity<I>
where
    I: 'static + IdentityManager,
    I::Identity: Serialize,
    <I as IdentityManager>::Error: IntoResponse,
{
    id_manager: Arc<RefCell<I>>,
}

impl<I> CreateIdentity<I>
where
    I: 'static + IdentityManager,
    I::Identity: Serialize,
    <I as IdentityManager>::Error: IntoResponse,
{
    pub fn new(id_manager: I) -> Self {
        CreateIdentity {
            id_manager: Arc::new(RefCell::new(id_manager)),
        }
    }
}

impl<I> Handler<Parameters> for CreateIdentity<I>
where
    I: 'static + IdentityManager,
    I::Identity: CoreIdentity + Serialize,
    <I as IdentityManager>::Error: IntoResponse,
{
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        let id_mgr = self.id_manager.clone();
        let response = read_request(req)
            .and_then(move |spec| {
                let mut rid = id_mgr.borrow_mut();
                rid.create(spec)
                    .map(|identity| {
                        let identity = Identity::new(
                            identity.module_id().to_string(),
                            identity.managed_by().to_string(),
                            identity.generation_id().to_string(),
                            identity.auth_type().to_string(),
                        );

                        serde_json::to_string(&identity)
                            .context(ErrorKind::Serde)
                            .map(|b| {
                                Response::builder()
                                    .status(StatusCode::OK)
                                    .header(CONTENT_TYPE, "application/json")
                                    .header(CONTENT_LENGTH, b.len().to_string().as_str())
                                    .body(b.into())
                                    .unwrap_or_else(|e| e.into_response())
                            })
                            .unwrap_or_else(|e| e.into_response())
                    })
                    .or_else(|e| future::ok(e.into_response()))
            })
            .or_else(|e| future::ok(e.into_response()) as FutureResult<Response<Body>, HyperError>);

        Box::new(response)
    }
}

fn read_request(req: Request<Body>) -> impl Future<Item = IdentitySpec, Error = Error> {
    req.into_body()
        .concat2()
        .map_err(Error::from)
        .and_then(|b| {
            serde_json::from_slice::<CreateIdentitySpec>(&b)
                .context(ErrorKind::BadBody)
                .map_err(Error::from)
        })
        .map(move |create_req| IdentitySpec::new(create_req.module_id()))
}

#[cfg(test)]
mod tests {
    use edgelet_core::AuthType;
    use edgelet_test_utils::identity::{TestIdentity, TestIdentityManager};
    use futures::Stream;
    use management::models::ErrorResponse;
    use serde_json::Value;

    use super::*;

    #[test]
    fn create_succeeds() {
        let manager = TestIdentityManager::new(vec![]);
        let handler = CreateIdentity::new(manager);
        let val = json!({ "moduleId": "m1" });
        let request = Request::post("http://localhost/identities")
            .body(serde_json::to_string(&val).unwrap().into())
            .unwrap();

        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();
        response
            .into_body()
            .concat2()
            .and_then(|body| {
                // make sure the JSON matches what we expect
                let json: Value = serde_json::from_slice(&body).unwrap();
                let expected_json = json!({
                    "moduleId": "m1",
                    "managedBy": "iotedge",
                    "generationId": "1",
                    "authType": "Sas",
                });
                assert_eq!(expected_json, json);

                let identity: TestIdentity = serde_json::from_slice(&body).unwrap();
                assert_eq!("m1", identity.module_id());
                assert_eq!("iotedge", identity.managed_by());
                assert_eq!("1", identity.generation_id());
                assert_eq!(AuthType::Sas, identity.auth_type());

                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn create_no_body() {
        let manager = TestIdentityManager::new(vec![]);
        let handler = CreateIdentity::new(manager);
        let request = Request::put("http://localhost/identities")
            .body(Body::default())
            .unwrap();
        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();

        assert_eq!(StatusCode::BAD_REQUEST, response.status());

        response
            .into_body()
            .concat2()
            .and_then(|body| {
                let error: ErrorResponse = serde_json::from_slice(&body).unwrap();
                assert_ne!(None, error.message().find("Bad body"));
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn create_fails() {
        let manager = TestIdentityManager::new(vec![]).with_fail_create(true);
        let handler = CreateIdentity::new(manager);
        let val = json!({ "moduleId": "m1" });
        let request = Request::put("http://localhost/identities")
            .body(serde_json::to_string(&val).unwrap().into())
            .unwrap();

        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());

        response
            .into_body()
            .concat2()
            .and_then(|body| {
                let error: ErrorResponse = serde_json::from_slice(&body).unwrap();
                assert_eq!("General error", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
