// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;

use failure::ResultExt;
use futures::{future, Future};
use hyper::{Error as HyperError, StatusCode};
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
use serde::Serialize;
use serde_json;

use edgelet_core::{IdentityManager, IdentitySpec};
use edgelet_http::route::{BoxFuture, Handler, Parameters};

use error::{Error, ErrorKind};
use IntoResponse;

pub struct CreateIdentity<I>
where
    I: 'static + IdentityManager,
    I::Identity: Serialize,
    <I as IdentityManager>::Error: IntoResponse,
{
    id_manager: RefCell<I>,
}

impl<I> CreateIdentity<I>
where
    I: 'static + IdentityManager,
    I::Identity: Serialize,
    <I as IdentityManager>::Error: IntoResponse,
{
    pub fn new(id_manager: I) -> Self {
        CreateIdentity {
            id_manager: RefCell::new(id_manager),
        }
    }
}

impl<I> Handler<Parameters> for CreateIdentity<I>
where
    I: 'static + IdentityManager,
    I::Identity: Serialize,
    <I as IdentityManager>::Error: IntoResponse,
{
    fn handle(&self, _req: Request, params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .map(|name| {
                let result = self.id_manager
                    .borrow_mut()
                    .create(IdentitySpec::new(name))
                    .map(|identity| {
                        serde_json::to_string(&identity)
                            .context(ErrorKind::Serde)
                            .map(|b| {
                                Response::new()
                                    .with_status(StatusCode::Ok)
                                    .with_header(ContentLength(b.len() as u64))
                                    .with_header(ContentType::json())
                                    .with_body(b)
                            })
                            .unwrap_or_else(|e| e.into_response())
                    })
                    .or_else(|e| future::ok(e.into_response()));
                future::Either::A(result)
            })
            .unwrap_or_else(|e| future::Either::B(future::ok(e.into_response())));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use futures::Stream;
    use hyper::{Method, Uri};
    use hyper::server::Request;

    use edgelet_core::Identity;
    use management::models::ErrorResponse;

    use server::identity::tests::*;

    use super::*;

    #[test]
    fn create_succeeds() {
        let manager = TestIdentityManager::new(vec![]);
        let handler = CreateIdentity::new(manager);
        let request = Request::new(
            Method::Put,
            Uri::from_str("http://localhost/identities").unwrap(),
        );
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "m1".to_string())]);

        let response = handler.handle(request, parameters).wait().unwrap();
        response
            .body()
            .concat2()
            .and_then(|body| {
                let identity: TestIdentity = serde_json::from_slice(&body).unwrap();
                assert_eq!("m1", identity.module_id());
                assert_eq!("iotedge", identity.managed_by());
                assert_eq!("1", identity.generation_id());

                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn create_no_name_param() {
        let manager = TestIdentityManager::new(vec![]);
        let handler = CreateIdentity::new(manager);
        let request = Request::new(
            Method::Put,
            Uri::from_str("http://localhost/identities").unwrap(),
        );
        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();
        response
            .body()
            .concat2()
            .and_then(|body| {
                let error: ErrorResponse = serde_json::from_slice(&body).unwrap();
                assert_eq!("Bad parameter", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn create_fails() {
        let manager = TestIdentityManager::new(vec![]).with_fail_create(true);
        let handler = CreateIdentity::new(manager);
        let request = Request::new(
            Method::Put,
            Uri::from_str("http://localhost/identities").unwrap(),
        );
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "m1".to_string())]);

        let response = handler.handle(request, parameters).wait().unwrap();
        response
            .body()
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
