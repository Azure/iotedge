// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use chrono::prelude::*;
use edgelet_core::WorkloadConfig;
use failure::ResultExt;
use futures::{Future, IntoFuture, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use aziot_key_common::KeyHandle;
use edgelet_core::crypto::Signature;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use identity_client::client::IdentityClient;

use crate::server::TokenHeader;
use crate::server::TokenClaims;

use super::get_derived_identity_key_handle;

use crate::error::{EncryptionOperation, Error, ErrorKind};
use crate::IntoResponse;


pub struct TokenValidator<W: WorkloadConfig> {
    key_client: Arc<aziot_key_client::Client>,
    identity_client: Arc<Mutex<IdentityClient>>,
    config: W,
}

impl<W: WorkloadConfig> TokenValidator<W> {
    pub fn new(
        key_client: Arc<aziot_key_client::Client>,
        identity_client: Arc<Mutex<IdentityClient>>,
        config: W,
    ) -> Self {
        TokenValidator {
            key_client,
            identity_client,
            config,
        }
    }
}

impl<W> Handler<Parameters> for TokenValidator<W>
where
    W: WorkloadConfig + Clone + Send + Sync + 'static,
{
    fn handle(
        &self,
        req: Request<Body>,
        params : Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {

        let id_mgr = self.identity_client.clone();

        let response = move {
            req.into_body().concat2().then(|body| {
            let body =
                body.context(ErrorKind::EncryptionOperation(EncryptionOperation::Encrypt))?;
            let request: TokenValidateRequest =
                serde_json::from_slice(&body).context(ErrorKind::MalformedRequestBody)?;
            Ok((request, id_mgr))
        })
    }
        .and_then(move |(request, id_mgr)| {
            let id = "ll".to_string();
            let id_mgr = self.identity_client.clone();

            req.into_body().concat2().then(|body| {
                let body =
                    body.context(ErrorKind::EncryptionOperation(EncryptionOperation::Encrypt))?;
                let request: TokenValidateRequest =
                    serde_json::from_slice(&body).context(ErrorKind::MalformedRequestBody)?;
                Ok((id, request, id_mgr))
            })
        })
        .into_future()
        .flatten()
        .and_then(move |(id, request, id_mgr)| -> Result<_, Error> {
            let data: Vec<u8> =
                base64::decode(request.data()).context(ErrorKind::MalformedRequestBody)?;
            let response = get_derived_identity_key_handle(&id_mgr, &id)
                .and_then(move |k| get_signature(&key_client, &k, &data))
                .and_then(|signature| -> Result<_, Error> {
                    let encoded = base64::encode(signature.as_bytes());
                    let response = SignResponse::new(encoded);
                    let body = serde_json::to_string(&response)
                        .context(ErrorKind::EncryptionOperation(EncryptionOperation::Sign))?;
                    let response = Response::builder()
                        .status(StatusCode::OK)
                        .header(CONTENT_TYPE, "application/json")
                        .header(CONTENT_LENGTH, body.len().to_string().as_str())
                        .body(body.into())
                        .context(ErrorKind::EncryptionOperation(EncryptionOperation::Sign))?;
                    Ok(response)
                });
            Ok(response)
        })
        .flatten()
        .or_else(|e| Ok(e.into_response()));

        Box::new(response)       
    }
}

use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenValidateRequest {
    token: String,
}