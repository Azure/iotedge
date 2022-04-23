// Copyright (c) Microsoft. All rights reserved.
use std::sync::Arc;

use anyhow::Context;
use futures::{Future, IntoFuture, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use aziot_key_common::EncryptMechanism;
use aziot_key_common::KeyHandle;
use edgelet_http::route::{Handler, Parameters};
use workload::models::{EncryptRequest, EncryptResponse};

use super::get_master_encryption_key;

use crate::error::{EncryptionOperation, Error};
use crate::IntoResponse;

pub struct EncryptHandler {
    key_client: Arc<aziot_key_client::Client>,
}

impl EncryptHandler {
    pub fn new(key_client: Arc<aziot_key_client::Client>) -> Self {
        EncryptHandler { key_client }
    }
}

impl Handler<Parameters> for EncryptHandler {
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        let key_client = self.key_client.clone();

        let response = params
            .name("name")
            .context(Error::MissingRequiredParameter("name"))
            .and_then(|name| {
                let genid = params
                    .name("genid")
                    .context(Error::MissingRequiredParameter("genid"))?;
                Ok((name, genid))
            })
            .map(|(module_id, genid)| {
                let id = format!("{}{}", module_id.to_string(), genid.to_string());
                req.into_body().concat2().then(|body| {
                    let body =
                        body.context(Error::EncryptionOperation(EncryptionOperation::Encrypt))?;
                    let request: EncryptRequest =
                        serde_json::from_slice(&body).context(Error::MalformedRequestBody)?;
                    Ok((id, request))
                })
            })
            .into_future()
            .flatten()
            .and_then(move |(id, request)| -> anyhow::Result<_> {
                let plaintext =
                    base64::decode(request.plaintext()).context(Error::MalformedRequestBody)?;
                let initialization_vector = base64::decode(request.initialization_vector())
                    .context(Error::MalformedRequestBody)?;
                let ciphertext = get_master_encryption_key(&key_client)
                    .and_then(|k| {
                        get_ciphertext(
                            key_client,
                            k,
                            initialization_vector,
                            id.into_bytes(),
                            plaintext,
                        )
                    })
                    .and_then(|ciphertext| -> anyhow::Result<_> {
                        let encoded = base64::encode(&ciphertext);
                        let response = EncryptResponse::new(encoded);
                        let body = serde_json::to_string(&response)
                            .context(Error::EncryptionOperation(EncryptionOperation::Encrypt))?;
                        let response = Response::builder()
                            .status(StatusCode::OK)
                            .header(CONTENT_TYPE, "application/json")
                            .header(CONTENT_LENGTH, body.len().to_string().as_str())
                            .body(body.into())
                            .context(Error::EncryptionOperation(EncryptionOperation::Encrypt))?;
                        Ok(response)
                    });
                Ok(ciphertext)
            })
            .flatten()
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

#[allow(clippy::needless_pass_by_value)]
fn get_ciphertext(
    key_client: Arc<aziot_key_client::Client>,
    key_handle: KeyHandle,
    iv: Vec<u8>,
    aad: Vec<u8>,
    plaintext: Vec<u8>,
) -> impl Future<Item = Vec<u8>, Error = anyhow::Error> {
    key_client
        .encrypt(&key_handle, EncryptMechanism::Aead { iv, aad }, &plaintext)
        .map_err(|_| anyhow::anyhow!(Error::GetIdentity))
        .into_future()
}
