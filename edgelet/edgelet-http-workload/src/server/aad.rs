// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::{future, Future, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use workload::models::{AADRequest, AADResponse};

use edgelet_http::route::{Handler, Parameters};
use edgelet_http::{Error as HttpError, ErrorKind as HttpErrorKind, IntoResponse};
use identity_client::client::IdentityClient;

// use crate::error::{Error, ErrorKind};

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
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        println!("Got aad request\n\n\n");
        let id_client = self.identity_client.clone();

        let response = req
            .into_body()
            .concat2()
            .then(|body| {
                println!("Parsing body");
                let body = body.unwrap(); //.map_err(|_e| HttpError::from(HttpErrorKind::AAD))?; // TODO: Error message
                let request: AADRequest = serde_json::from_slice(&body).unwrap(); //.map_err(|_e| HttpError::from(HttpErrorKind::AAD))?;
                println!("Parsed body {:#?}", request);

                Ok(request)
            })
            .and_then(move |request| {
                let id_client = id_client.lock().unwrap();
                id_client
                    .get_aad_token(request.tenant(), request.scope(), request.aad_id())
                    .map_err(|_e| HttpError::from(HttpErrorKind::AAD)) // TODO: Error message
            })
            .and_then(|token: String| {
                println!("Got Token {}", token);

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
    }
}
