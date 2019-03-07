// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::Future;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use serde::Serialize;
use serde_json;

use edgelet_core::{Identity as CoreIdentity, IdentityManager, IdentityOperation};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use management::models::{Identity, IdentityList};

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct ListIdentities<I> {
    id_manager: I,
}

impl<I> ListIdentities<I> {
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
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let response = self
            .id_manager
            .list()
            .then(|result| -> Result<_, Error> {
                let identities = result.context(ErrorKind::IdentityOperation(
                    IdentityOperation::ListIdentities,
                ))?;
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
                        })
                        .collect(),
                );
                let b = serde_json::to_string(&body).context(ErrorKind::IdentityOperation(
                    IdentityOperation::ListIdentities,
                ))?;
                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, b.len().to_string().as_str())
                    .body(b.into())
                    .context(ErrorKind::IdentityOperation(
                        IdentityOperation::ListIdentities,
                    ))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()));

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
            })
            .wait()
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
                    "Could not list identities\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
