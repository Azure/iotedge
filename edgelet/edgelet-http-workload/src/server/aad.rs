// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::{future, Future};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use workload::models::AADResponse;

use edgelet_http::route::{Handler, Parameters};
use edgelet_http::{Error as HttpError, ErrorKind as HttpErrorKind, IntoResponse};
use identity_client::client::IdentityClient;

use crate::error::{Error, ErrorKind};

pub struct AADHandler {
    identity_client: Arc<Mutex<IdentityClient>>,
}

impl AADHandler {
    pub fn new(identity_client: Arc<Mutex<IdentityClient>>) -> Self {
        AADHandler { identity_client }
    }
}

impl Handler<Parameters> for AADHandler {
    fn handle(
        &self,
        _req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let id_client = self.identity_client.clone();
        let id_client = id_client.lock().unwrap();

        let params: Result<(String, String, String), Error> = params
            .name("tenant")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("tenant")))
            .and_then(|tenant| {
                let scope = params
                    .name("scope")
                    .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("scope")))?;
                Ok((tenant, scope))
            })
            .and_then(|(tenant, scope)| {
                let aad_id = params
                    .name("aad_id")
                    .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("aad_id")))?;
                Ok((tenant.to_owned(), scope.to_owned(), aad_id.to_owned()))
            });

        if let Ok((tenant, scope, aad_id)) = params {
            let response = id_client
                .get_aad_token(&tenant, &scope, &aad_id)
                .map_err(|_e| HttpError::from(HttpErrorKind::AAD)) // TODO: Error message
                .and_then(|token: String| {
                    let response = AADResponse::new(token);
                    let body = serde_json::to_string(&response).context(HttpErrorKind::AAD)?;

                    let response: Response<Body> = Response::builder()
                        .status(StatusCode::OK)
                        .header(CONTENT_TYPE, "application/json")
                        .header(CONTENT_LENGTH, body.len().to_string().as_str())
                        .body(body.into())
                        .context(HttpErrorKind::AAD)?;
                    Ok(response)
                })
                .or_else(|e| future::ok(e.into_response()));

            Box::new(response)
        } else {
            Box::new(futures::future::err(HttpError::from(HttpErrorKind::AAD))) // TODO: Error message
        }
    }
}
