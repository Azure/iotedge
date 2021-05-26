// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::{Future, IntoFuture, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use workload::models::{SignRequest, SignResponse};

use aziot_key_common::KeyHandle;
use edgelet_core::crypto::Signature;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use identity_client::client::IdentityClient;

use super::get_derived_identity_key_handle;

use crate::error::{EncryptionOperation, Error, ErrorKind};
use crate::IntoResponse;

pub struct SignHandler {
    key_client: Arc<aziot_key_client::Client>,
    identity_client: Arc<Mutex<IdentityClient>>,
}

impl SignHandler {
    pub fn new(
        key_client: Arc<aziot_key_client::Client>,
        identity_client: Arc<Mutex<IdentityClient>>,
    ) -> Self {
        SignHandler {
            key_client,
            identity_client,
        }
    }
}

impl Handler<Parameters> for SignHandler {
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let key_client = self.key_client.clone();

        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .and_then(|name| {
                let genid = params
                    .name("genid")
                    .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("genid")))?;
                Ok((name, genid))
            })
            .map(move |(name, _)| {
                let id = name.to_string();
                let id_mgr = self.identity_client.clone();

                req.into_body().concat2().then(|body| {
                    let body =
                        body.context(ErrorKind::EncryptionOperation(EncryptionOperation::Encrypt))?;
                    let request: SignRequest =
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

fn get_signature(
    key_client: &Arc<aziot_key_client::Client>,
    key_handle: &KeyHandle,
    data: &[u8],
) -> impl Future<Item = Vec<u8>, Error = Error> {
    key_client
        .sign(
            &key_handle,
            aziot_key_common::SignMechanism::HmacSha256,
            data.as_bytes(),
        )
        .map_err(|_| Error::from(ErrorKind::GetIdentity))
        .into_future()
}
