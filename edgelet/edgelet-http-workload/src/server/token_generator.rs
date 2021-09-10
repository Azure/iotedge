// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use chrono::prelude::*;
use edgelet_core::WorkloadConfig;
use failure::ResultExt;
use futures::{Future, IntoFuture};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use aziot_key_common::KeyHandle;
use edgelet_core::crypto::Signature;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use identity_client::client::IdentityClient;


use super::get_derived_identity_key_handle;

use crate::error::{EncryptionOperation, Error, ErrorKind};
use crate::IntoResponse;
use crate::server::TokenHeader;
use crate::server::TokenClaims;

pub struct TokenGenerator<W: WorkloadConfig> {
    key_client: Arc<aziot_key_client::Client>,
    identity_client: Arc<Mutex<IdentityClient>>,
    config: W,
    module_id: String,
}

impl<W: WorkloadConfig> TokenGenerator<W> {
    pub fn new(
        key_client: Arc<aziot_key_client::Client>,
        identity_client: Arc<Mutex<IdentityClient>>,
        config: W,
        module_id: String,
    ) -> Self {
        TokenGenerator {
            key_client,
            identity_client,
            config,
            module_id,
        }
    }
}

impl<W> Handler<Parameters> for TokenGenerator<W>
where
    W: WorkloadConfig + Clone + Send + Sync + 'static,
{
    fn handle(
        &self,
        _req: Request<Body>,
        params : Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let config = self.config.clone();
        let device_id = config.device_id().to_string();
      
        let key_client = self.key_client.clone();
        let iot_hub = config.iot_hub_name().to_string();
        
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .map(move |name| {
                let module_id = name.to_string();
                let id_mgr = self.identity_client.clone();

                if self.module_id.eq(&module_id) {
                    Ok((module_id, id_mgr))
                }else
                {
                    Err(Error::from(ErrorKind::TokenAuthError))
                }
            })
            .into_future()
            .flatten()
            .and_then(move |(module_id, id_mgr)| -> Result<_, Error> {

                let local: DateTime<Local> = Local::now();

                // Expire in 1h. TO DO set that as a parameter
                let exp = local.second()+3600;

                let sub = format!("spiffe://{}/{}/{}", &iot_hub, &device_id, module_id);

                let header = TokenHeader {
                    alg: "HS256".to_string(),
                };
                let header = serde_json::to_string(&header).context(ErrorKind::JsonConvertError)?;
                let header = base64::encode_config(header.as_bytes(), base64::STANDARD_NO_PAD);
                
                let claim = TokenClaims {
                    sub,
                    aud: params.name("name").unwrap().to_string(),
                    exp,
                    aziot_id: None,
                };
                let claim = serde_json::to_string(&claim).context(ErrorKind::JsonConvertError)?;
                let claim = base64::encode_config(claim.as_bytes(), base64::STANDARD_NO_PAD);
                
                let signature = format!("{}.{}",header, claim);

                let response = get_derived_identity_key_handle(&id_mgr, &module_id)
                    .and_then(move |k| get_signature(&key_client, &k, &signature.as_bytes()))
                    .and_then(move |signature| -> Result<_, Error> {
                        
                        let signature = base64::encode_config(signature.as_bytes(), base64::STANDARD_NO_PAD);  

                        let payload = format!("{}.{}.{}", header, claim, signature);
                        let response = TokenGenerateResponse::new(payload);

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



use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenGenerateResponse {
    token: String,
}

impl TokenGenerateResponse {
    pub fn new(token: String) -> Self {
        TokenGenerateResponse { token }
    }
}
