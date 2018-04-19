// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;

use futures::{future, Future};
use hyper::{Error as HyperError, StatusCode};
use hyper::server::{Request, Response};

use edgelet_core::{IdentityManager, IdentitySpec};
use edgelet_http::route::{BoxFuture, Handler, Parameters};

use error::{Error, ErrorKind};
use IntoResponse;

pub struct DeleteIdentity<I>
where
    I: 'static + IdentityManager,
    <I as IdentityManager>::Error: IntoResponse,
{
    id_manager: RefCell<I>,
}

impl<I> DeleteIdentity<I>
where
    I: 'static + IdentityManager,
    <I as IdentityManager>::Error: IntoResponse,
{
    pub fn new(id_manager: I) -> Self {
        DeleteIdentity {
            id_manager: RefCell::new(id_manager),
        }
    }
}

impl<I> Handler<Parameters> for DeleteIdentity<I>
where
    I: 'static + IdentityManager,
    <I as IdentityManager>::Error: IntoResponse,
{
    fn handle(&self, _req: Request, params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .map(|name| {
                let result = self.id_manager
                    .borrow_mut()
                    .delete(IdentitySpec::new(name))
                    .map(|_| Response::new().with_status(StatusCode::NoContent))
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
    use serde_json;

    use edgelet_core::Identity;
    use management::models::ErrorResponse;

    use server::identity::tests::*;

    use super::*;

    #[test]
    fn delete_succeeds() {
        let manager = TestIdentityManager::new(vec![
            TestIdentity::new("m1", "iotedge", "1"),
            TestIdentity::new("m2", "iotedge", "2"),
            TestIdentity::new("m3", "iotedge", "3"),
        ]);
        let handler = DeleteIdentity::new(manager);
        let request = Request::new(
            Method::Put,
            Uri::from_str("http://localhost/identities").unwrap(),
        );
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "m2".to_string())]);

        let response = handler.handle(request, parameters).wait().unwrap();
        assert_eq!(StatusCode::NoContent, response.status());

        let list = handler.id_manager.borrow().get().wait().unwrap();
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
    fn delete_fails() {
        let manager = TestIdentityManager::new(vec![]).with_fail_create(true);
        let handler = DeleteIdentity::new(manager);
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
                assert_eq!("Module not found", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
