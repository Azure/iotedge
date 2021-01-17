// Copyright (c) Microsoft. All rights reserved.
use std::sync::Arc;

use failure::ResultExt;
use futures::{Future, IntoFuture, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use aziot_key_common::EncryptMechanism;
use aziot_key_common::KeyHandle;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use workload::models::{EncryptRequest, EncryptResponse};

use super::get_derived_enc_key_handle;

use crate::error::{EncryptionOperation, Error, ErrorKind};
use crate::IntoResponse;

pub struct EncryptHandler {
    key_connector: http_common::Connector,
}

impl EncryptHandler {
    pub fn new(key_connector: http_common::Connector) -> Self {
        EncryptHandler { key_connector }
    }
}

impl Handler<Parameters> for EncryptHandler {
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let key_connector = self.key_connector.clone();

        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .and_then(|name| {
                let genid = params
                    .name("genid")
                    .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("genid")))?;
                Ok((name, genid))
            })
            .map(|(module_id, genid)| {
                let id = format!("{}{}", module_id.to_string(), genid.to_string());
                req.into_body().concat2().then(|body| {
                    let body =
                        body.context(ErrorKind::EncryptionOperation(EncryptionOperation::Encrypt))?;
                    let request: EncryptRequest =
                        serde_json::from_slice(&body).context(ErrorKind::MalformedRequestBody)?;
                    Ok((id, request))
                })
            })
            .into_future()
            .flatten()
            .and_then(move |(id, request)| -> Result<_, Error> {
                let key_client = {
                    let key_client = aziot_key_client::Client::new(
                        aziot_key_common_http::ApiVersion::V2020_09_01,
                        key_connector,
                    );
                    let key_client = Arc::new(key_client);
                    key_client
                };

                let plaintext =
                    base64::decode(request.plaintext()).context(ErrorKind::MalformedRequestBody)?;
                let initialization_vector = base64::decode(request.initialization_vector())
                    .context(ErrorKind::MalformedRequestBody)?;
                let ciphertext = get_derived_enc_key_handle(key_client.clone(), id.clone())
                    .and_then(|k| {
                        get_ciphertext(
                            key_client,
                            k,
                            initialization_vector,
                            id.into_bytes(),
                            plaintext,
                        )
                    })
                    .and_then(|ciphertext| -> Result<_, Error> {
                        let encoded = base64::encode(&ciphertext);
                        let response = EncryptResponse::new(encoded);
                        let body = serde_json::to_string(&response).context(
                            ErrorKind::EncryptionOperation(EncryptionOperation::Encrypt),
                        )?;
                        let response = Response::builder()
                            .status(StatusCode::OK)
                            .header(CONTENT_TYPE, "application/json")
                            .header(CONTENT_LENGTH, body.len().to_string().as_str())
                            .body(body.into())
                            .context(ErrorKind::EncryptionOperation(
                                EncryptionOperation::Encrypt,
                            ))?;
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
) -> impl Future<Item = Vec<u8>, Error = Error> {
    key_client
        .encrypt(&key_handle, EncryptMechanism::Aead { iv, aad }, &plaintext)
        .map_err(|_| Error::from(ErrorKind::GetIdentity))
        .into_future()
}
