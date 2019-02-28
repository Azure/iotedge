// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::{Future, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use serde::Serialize;
use serde_json;

use edgelet_core::{Identity as CoreIdentity, IdentityManager, IdentityOperation, IdentitySpec};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use management::models::{Identity, IdentitySpec as CreateIdentitySpec};

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct CreateIdentity<I> {
    id_manager: Arc<Mutex<I>>,
}

impl<I> CreateIdentity<I> {
    pub fn new(id_manager: I) -> Self {
        CreateIdentity {
            id_manager: Arc::new(Mutex::new(id_manager)),
        }
    }
}

impl<I> Handler<Parameters> for CreateIdentity<I>
where
    I: 'static + IdentityManager + Send,
    I::Identity: CoreIdentity + Serialize,
{
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let id_mgr = self.id_manager.clone();
        let response = read_request(req)
            .and_then(move |spec| {
                let mut rid = id_mgr.lock().unwrap();

                let module_id = spec.module_id().to_string();

                rid.create(spec).then(|identity| -> Result<_, Error> {
                    let identity = identity.with_context(|_| {
                        ErrorKind::IdentityOperation(IdentityOperation::CreateIdentity(module_id))
                    })?;

                    let module_id = identity.module_id().to_string();

                    let identity = Identity::new(
                        module_id.clone(),
                        identity.managed_by().to_string(),
                        identity.generation_id().to_string(),
                        identity.auth_type().to_string(),
                    );

                    let b = serde_json::to_string(&identity).with_context(|_| {
                        ErrorKind::IdentityOperation(IdentityOperation::CreateIdentity(
                            module_id.clone(),
                        ))
                    })?;
                    let response = Response::builder()
                        .status(StatusCode::OK)
                        .header(CONTENT_TYPE, "application/json")
                        .header(CONTENT_LENGTH, b.len().to_string().as_str())
                        .body(b.into())
                        .with_context(|_| {
                            ErrorKind::IdentityOperation(IdentityOperation::CreateIdentity(
                                module_id,
                            ))
                        })?;
                    Ok(response)
                })
            })
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

fn read_request(req: Request<Body>) -> impl Future<Item = IdentitySpec, Error = Error> {
    req.into_body().concat2().then(|b| {
        let b = b.context(ErrorKind::MalformedRequestBody)?;
        let create_req = serde_json::from_slice::<CreateIdentitySpec>(&b)
            .context(ErrorKind::MalformedRequestBody)?;
        let mut spec = IdentitySpec::new(create_req.module_id().to_string());
        if let Some(m) = create_req.managed_by() {
            spec = spec.with_managed_by(m.to_string());
        }
        Ok(spec)
    })
}

#[cfg(test)]
mod tests {
    use edgelet_core::AuthType;
    use edgelet_test_utils::identity::{TestIdentity, TestIdentityManager};
    use futures::Stream;
    use management::models::ErrorResponse;
    use serde_json::{json, Value};

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
                    "managedBy": "",
                    "generationId": "1",
                    "authType": "Sas",
                });
                assert_eq!(expected_json, json);

                let identity: TestIdentity = serde_json::from_slice(&body).unwrap();
                assert_eq!("m1", identity.module_id());
                assert_eq!("", identity.managed_by());
                assert_eq!("1", identity.generation_id());
                assert_eq!(AuthType::Sas, identity.auth_type());

                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn create_with_managed_by_succeeds() {
        let manager = TestIdentityManager::new(vec![]);
        let handler = CreateIdentity::new(manager);
        let val = json!({ "moduleId": "m1", "managedBy": "foo" });
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
                    "managedBy": "foo",
                    "generationId": "1",
                    "authType": "Sas",
                });
                assert_eq!(expected_json, json);

                let identity: TestIdentity = serde_json::from_slice(&body).unwrap();
                assert_eq!("m1", identity.module_id());
                assert_eq!("foo", identity.managed_by());
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
                assert_eq!("Request body is malformed\n\tcaused by: EOF while parsing a value at line 1 column 0", error.message());
                Ok(())
            }).wait()
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
                assert_eq!(
                    "Could not create identity m1\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
