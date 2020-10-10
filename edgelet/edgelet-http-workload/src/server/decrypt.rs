// Copyright (c) Microsoft. All rights reserved.
use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::{future, Future, IntoFuture, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use aziot_key_common::EncryptMechanism;
use aziot_key_common::KeyHandle;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use identity_client::client::IdentityClient;
use workload::models::{DecryptRequest, DecryptResponse};

use super::get_key_handle;

use crate::error::{EncryptionOperation, Error, ErrorKind};
use crate::IntoResponse;

pub struct DecryptHandler {
    key_client: Arc<Mutex<aziot_key_client::Client>>,
    identity_client: Arc<Mutex<IdentityClient>>,
}

impl DecryptHandler {
    pub fn new(key_client: Arc<Mutex<aziot_key_client::Client>>, identity_client: Arc<Mutex<IdentityClient>>) -> Self {
        DecryptHandler { key_client, identity_client }
    }
}

impl Handler<Parameters> for DecryptHandler
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let key_store = self.key_client.clone();
        let id_mgr = self.identity_client.clone();

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
                        body.context(ErrorKind::EncryptionOperation(EncryptionOperation::Decrypt))?;
                    let request: DecryptRequest =
                        serde_json::from_slice(&body).context(ErrorKind::MalformedRequestBody)?;
                    Ok((id, request))
                })
            })
            .into_future()
            .flatten()
            .and_then(move |(id, request)| -> Result<_, Error> {
                let plaintext =
                    base64::decode(request.ciphertext()).context(ErrorKind::MalformedRequestBody)?;
                let initialization_vector = base64::decode(request.initialization_vector())
                    .context(ErrorKind::MalformedRequestBody)?;
                let ciphertext = get_key_handle(id_mgr, &id)
                    .and_then(|k| { 
                        get_plaintext(key_store, k, initialization_vector, plaintext)
                    })
                    .and_then(|plaintext| -> Result<_, Error> {
                        let encoded = base64::encode(&plaintext);
                        let response = DecryptResponse::new(encoded);
                        let body = serde_json::to_string(&response)
                            .context(ErrorKind::EncryptionOperation(EncryptionOperation::Decrypt))?;
                        let response = Response::builder()
                            .status(StatusCode::OK)
                            .header(CONTENT_TYPE, "application/json")
                            .header(CONTENT_LENGTH, body.len().to_string().as_str())
                            .body(body.into())
                            .context(ErrorKind::EncryptionOperation(EncryptionOperation::Decrypt))?;
                        Ok(response)
                    });
                Ok(ciphertext)
            })
            .flatten()
            .or_else(|e| future::ok(e.into_response()));

        Box::new(response)
    }
}

fn get_plaintext(key_client: Arc<Mutex<aziot_key_client::Client>>, key_handle: KeyHandle, iv: Vec<u8>, ciphertext: Vec<u8>) -> impl Future<Item = Vec<u8>, Error = Error> {
	let aad = b"$iotedged".to_vec();
    
    key_client
    .lock()
    .expect("lock error")
    .decrypt(
        &key_handle,
         EncryptMechanism::Aead {
             iv,
             aad
         },
         &ciphertext)
    .map_err(|_| Error::from(ErrorKind::GetIdentity))
    .into_future()
}
