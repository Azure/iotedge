// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::{future, Future};
use hyper::{Error as HyperError, StatusCode};
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
use serde::Serialize;
use serde_json;

use edgelet_core::{Identity as CoreIdentity, IdentityManager};
use edgelet_http::route::{BoxFuture, Handler, Parameters};

use error::ErrorKind;
use IntoResponse;
use management::models::{Identity, IdentityList};

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
    I: 'static + IdentityManager,
    I::Identity: Serialize,
{
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = self.id_manager.get().then(|result| {
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
                                )
                            })
                            .collect(),
                    );
                    let result = serde_json::to_string(&body)
                        .context(ErrorKind::Serde)
                        .map(|b| {
                            Response::new()
                                .with_status(StatusCode::Ok)
                                .with_header(ContentLength(b.len() as u64))
                                .with_header(ContentType::json())
                                .with_body(b)
                        })
                        .unwrap_or_else(|e| e.into_response());

                    future::ok(result)
                })
                .unwrap_or_else(|e| future::ok(e.into_response()))
        });

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use futures::Stream;
    use hyper::{Method, Uri};
    use hyper::server::Request;

    use management::models::ErrorResponse;

    use server::identity::tests::*;

    use super::*;

    #[test]
    fn list_succeeds() {
        let manager = TestIdentityManager::new(vec![
            TestIdentity::new("m1", "iotedge", "1"),
            TestIdentity::new("m2", "iotedge", "2"),
            TestIdentity::new("m3", "iotedge", "3"),
        ]);
        let handler = ListIdentities::new(manager);
        let request = Request::new(
            Method::Get,
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
                let list: IdentityList = serde_json::from_slice(&body).unwrap();
                for i in 1..4 {
                    assert_eq!(&format!("m{}", i), list.identities()[i - 1].module_id());
                    assert_eq!("iotedge", list.identities()[i - 1].managed_by());
                    assert_eq!(&format!("{}", i), list.identities()[i - 1].generation_id());
                }

                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn list_fails() {
        let manager = TestIdentityManager::new(vec![]).with_fail_get(true);
        let handler = ListIdentities::new(manager);
        let request = Request::new(
            Method::Get,
            Uri::from_str("http://localhost/modules").unwrap(),
        );
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        response
            .body()
            .concat2()
            .and_then(|body| {
                let error: ErrorResponse = serde_json::from_slice(&body).unwrap();
                assert_eq!(
                    "Identity manager error\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
