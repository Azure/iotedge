// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use failure::{Fail, ResultExt};
use futures::{Future, IntoFuture, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use serde::Serialize;
use serde_json;

use edgelet_core::{Identity as CoreIdentity, IdentityManager, IdentityOperation, IdentitySpec};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use management::models::{Identity, UpdateIdentity as UpdateIdentityRequest};

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct UpdateIdentity<I> {
    id_manager: Arc<Mutex<I>>,
}

impl<I> UpdateIdentity<I> {
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
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let id_manager = self.id_manager.clone();

        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .map(|name| {
                let name = name.to_string();
                read_request(name.clone(), req).map(|spec| (spec, name))
            })
            .into_future()
            .flatten()
            .and_then(move |(spec, name)| {
                let mut rid = id_manager.lock().unwrap();
                rid.update(spec).map_err(|err| {
                    Error::from(err.context(ErrorKind::IdentityOperation(
                        IdentityOperation::UpdateIdentity(name),
                    )))
                })
            })
            .and_then(|id| Ok(write_response(&id)))
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

fn write_response<I>(identity: &I) -> Response<Body>
where
    I: 'static + CoreIdentity + Serialize,
{
    let module_id = identity.module_id().to_string();
    let identity = Identity::new(
        module_id.clone(),
        identity.managed_by().to_string(),
        identity.generation_id().to_string(),
        identity.auth_type().to_string(),
    );

    serde_json::to_string(&identity)
        .with_context(|_| {
            ErrorKind::IdentityOperation(IdentityOperation::UpdateIdentity(module_id.clone()))
        })
        .map_err(Error::from)
        .and_then(|b| {
            Ok(Response::builder()
                .status(StatusCode::OK)
                .header(CONTENT_TYPE, "application/json")
                .header(CONTENT_LENGTH, b.len().to_string().as_str())
                .body(b.into())
                .context(ErrorKind::IdentityOperation(
                    IdentityOperation::UpdateIdentity(module_id),
                ))?)
        })
        .unwrap_or_else(|e| e.into_response())
}

fn read_request(
    name: String,
    req: Request<Body>,
) -> impl Future<Item = IdentitySpec, Error = Error> {
    req.into_body().concat2().then(move |b| {
        let b = b.context(ErrorKind::MalformedRequestBody)?;
        let update_req = serde_json::from_slice::<UpdateIdentityRequest>(&b)
            .context(ErrorKind::MalformedRequestBody)?;
        let mut spec =
            IdentitySpec::new(name).with_generation_id(update_req.generation_id().to_string());
        if let Some(m) = update_req.managed_by() {
            spec = spec.with_managed_by(m.to_string());
        }
        Ok(spec)
    })
}

#[cfg(test)]
mod tests {
    use edgelet_core::AuthType;
    use edgelet_test_utils::identity::{TestIdentity, TestIdentityManager};
    use management::models::ErrorResponse;
    use serde_json::{json, Value};

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
            })
            .wait()
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
                assert_eq!("Request body is malformed\n\tcaused by: EOF while parsing a value at line 1 column 0", error.message());
                Ok(())
            }).wait()
            .unwrap();
    }
}
