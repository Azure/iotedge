// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::Future;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use workload::models::AADResponse;

use edgelet_http::route::{Handler, Parameters};
use edgelet_http::{Error, ErrorKind};
use identity_client::client::IdentityClient;

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
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = Error> + Send> {
        let id_client = self.identity_client.clone();
        let id_client = id_client.lock().unwrap();

        let response = id_client
            .get_aad_token()
            .map_err(|_e| Error::from(ErrorKind::AAD)) // TODO: Error message
            .and_then(|token: String| {
                let response = AADResponse::new(token);
                let body = serde_json::to_string(&response).context(ErrorKind::AAD)?;

                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, body.len().to_string().as_str())
                    .body(body.into())
                    .context(ErrorKind::AAD)?;
                Ok(response)
            });

        Box::new(response)
    }
}
