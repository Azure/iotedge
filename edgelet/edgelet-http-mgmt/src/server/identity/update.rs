// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::future::{self, Either, FutureResult};
use futures::{Future, Stream};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use serde::Serialize;
use serde_json;

use edgelet_core::{Identity as CoreIdentity, IdentityManager, IdentitySpec};
use edgelet_http::route::{Handler, Parameters};
use management::models::{Identity, UpdateIdentity as UpdateIdentityRequest};

use error::{Error, ErrorKind};
use IntoResponse;

pub struct UpdateIdentity<I>
where
    I: 'static + IdentityManager,
    I::Identity: Serialize,
    <I as IdentityManager>::Error: IntoResponse,
{
    id_manager: Arc<Mutex<I>>,
}

impl<I> UpdateIdentity<I>
where
    I: 'static + IdentityManager,
    I::Identity: Serialize,
    <I as IdentityManager>::Error: IntoResponse,
{
    pub fn new(id_manager: I) -> Self {
        UpdateIdentity {
            id_manager: Arc::new(Mutex::new(id_manager)),
        }
    }
}

impl<I> Handler<Parameters> for UpdateIdentity<I>
where
    I: 'static + IdentityManager + Send,
    I::Identity: CoreIdentity + Serialize,
    <I as IdentityManager>::Error: IntoResponse,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
        let id_manager = self.id_manager.clone();
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .map(|name| {
                let result = read_request(name, req)
                    .and_then(move |spec| {
                        let mut rid = id_manager.lock().unwrap();
                        rid.update(spec)
                            .map(|id| write_response(&id))
                            .or_else(|e| future::ok(e.into_response()))
                    }).or_else(|e| {
                        future::ok(e.into_response()) as FutureResult<Response<Body>, HyperError>
                    });

                Either::A(result)
            }).unwrap_or_else(|e| Either::B(future::ok(e.into_response())));

        Box::new(response)
    }
}

fn write_response<I>(identity: &I) -> Response<Body>
where
    I: 'static + CoreIdentity + Serialize,
{
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
        }).unwrap_or_else(|e| e.into_response())
}

fn read_request(name: &str, req: Request<Body>) -> impl Future<Item = IdentitySpec, Error = Error> {
    let name = name.to_string();
    req.into_body()
        .concat2()
        .map_err(Error::from)
        .and_then(|b| {
            serde_json::from_slice::<UpdateIdentityRequest>(&b)
                .context(ErrorKind::BadBody)
                .map_err(Error::from)
        }).map(move |update_req| {
            let mut spec =
                IdentitySpec::new(&name).with_generation_id(update_req.generation_id().to_string());
            if let Some(m) = update_req.managed_by() {
                spec = spec.with_managed_by(m.to_string());
            }
            spec
        })
}

#[cfg(test)]
mod tests {
    use edgelet_core::AuthType;
    use edgelet_test_utils::identity::{TestIdentity, TestIdentityManager};
    use management::models::ErrorResponse;
    use serde_json::Value;

    use super::*;

    #[test]
    fn update() {
        let manager = TestIdentityManager::new(vec![TestIdentity::new(
            "m1",
            "iotedge",
            "g1",
            AuthType::Sas,
        )]);
        let handler = UpdateIdentity::new(manager);
        let update_req =
            UpdateIdentityRequest::new("g1".to_string()).with_managed_by("iotedge".to_string());
        let request = Request::put("http://localhost/identities")
            .body(serde_json::to_string(&update_req).unwrap().into())
            .unwrap();

        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "m1".to_string())]);
        let response = handler.handle(request, parameters).wait().unwrap();

        assert_eq!(StatusCode::OK, response.status());
        assert_eq!("76", *response.headers().get(CONTENT_LENGTH).unwrap());
        assert_eq!(
            "application/json",
            *response.headers().get(CONTENT_TYPE).unwrap()
        );

        response
            .into_body()
            .concat2()
            .and_then(|body| {
                // make sure the JSON matches what we expect
                let json: Value = serde_json::from_slice(&body).unwrap();
                let expected_json = json!({
                    "moduleId": "m1",
                    "managedBy": "iotedge",
                    "generationId": "g1",
                    "authType": "Sas",
                });
                assert_eq!(expected_json, json);

                let identity: TestIdentity = serde_json::from_slice(&body).unwrap();
                assert_eq!("m1", identity.module_id());
                assert_eq!("iotedge", identity.managed_by());
                assert_eq!("g1", identity.generation_id());
                assert_eq!(AuthType::Sas, identity.auth_type());

                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn update_no_name() {
        let manager = TestIdentityManager::new(vec![TestIdentity::new(
            "m1",
            "iotedge",
            "g1",
            AuthType::Sas,
        )]);
        let handler = UpdateIdentity::new(manager);
        let update_req = UpdateIdentityRequest::new("g1".to_string());
        let request = Request::put("http://localhost/identities")
            .body(serde_json::to_string(&update_req).unwrap().into())
            .unwrap();

        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        assert_eq!(StatusCode::BAD_REQUEST, response.status());

        response
            .into_body()
            .concat2()
            .and_then(|body| {
                let error: ErrorResponse = serde_json::from_slice(&body).unwrap();
                assert_eq!("Bad parameter", error.message());
                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn update_bad_body() {
        let manager = TestIdentityManager::new(vec![TestIdentity::new(
            "m1",
            "iotedge",
            "g1",
            AuthType::Sas,
        )]);
        let handler = UpdateIdentity::new(manager);
        let request = Request::put("http://localhost/identities")
            .body(Body::default())
            .unwrap();
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "m1".to_string())]);

        let response = handler.handle(request, parameters).wait().unwrap();

        assert_eq!(StatusCode::BAD_REQUEST, response.status());

        response
            .into_body()
            .concat2()
            .and_then(|body| {
                let error: ErrorResponse = serde_json::from_slice(&body).unwrap();
                assert_ne!(None, error.message().find("Bad body"));
                Ok(())
            }).wait()
            .unwrap();
    }
}
