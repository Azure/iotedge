// Copyright (c) Microsoft. All rights reserved.

use base64;
use chrono::prelude::*;
use failure::ResultExt;
use futures::{future, Future, Stream};
use http::{Request, Response, StatusCode};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Error as HyperError};
use serde_json;

use edgelet_http::route::{BoxFuture, Handler, Parameters};
use hsm::{CertificateProperties, CertificateType, CreateCertificate, HsmCertificate, PrivateKey};
use workload::models::{CertificateResponse, PrivateKey as PrivateKeyResponse,
                       ServerCertificateRequest};

use error::{Error, ErrorKind, Result};
use IntoResponse;

pub struct ServerCertHandler<T: CreateCertificate> {
    hsm: T,
}

impl<T: CreateCertificate> ServerCertHandler<T> {
    pub fn new(hsm: T) -> Self {
        ServerCertHandler { hsm }
    }
}

impl<T> Handler<Parameters> for ServerCertHandler<T>
where
    T: CreateCertificate + 'static + Clone,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        let hsm = self.hsm.clone();
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .map(|_module_id| {
                let result = req.into_body()
                    .concat2()
                    .map(|body| {
                        serde_json::from_slice::<ServerCertificateRequest>(&body)
                            .context(ErrorKind::BadBody)
                            .map_err(Error::from)
                            .and_then(|cert_req| {
                                compute_validity(ensure_not_empty!(cert_req.expiration()).as_str())
                                    .map(|expiration| (cert_req, expiration))
                            })
                            .and_then(move |(cert_req, expiration)| {
                                let props = CertificateProperties::default()
                                    .with_common_name(
                                        ensure_not_empty!(cert_req.common_name()).as_str(),
                                    )
                                    .with_validity_in_secs(ensure_range!(
                                        expiration,
                                        0,
                                        i64::max_value()
                                    ))
                                    .with_certificate_type(CertificateType::Server);
                                hsm.create_certificate(&props)
                                    .map_err(Error::from)
                                    .and_then(|cert| {
                                        let private_key = cert_to_response(
                                            &cert,
                                            cert_req.expiration().as_str(),
                                        )?;
                                        let body = serde_json::to_string(&private_key)?;
                                        Response::builder()
                                            .status(StatusCode::CREATED)
                                            .header(CONTENT_TYPE, "application/json")
                                            .header(CONTENT_LENGTH, body.len().to_string().as_str())
                                            .body(body.into())
                                            .map_err(From::from)
                                    })
                            })
                            .unwrap_or_else(|e| e.into_response())
                    })
                    .map_err(Error::from)
                    .or_else(|e| future::ok(e.into_response()));

                future::Either::A(result)
            })
            .unwrap_or_else(|e| future::Either::B(future::ok(e.into_response())));

        Box::new(response)
    }
}

fn cert_to_response(cert: &HsmCertificate, expiration: &str) -> Result<CertificateResponse> {
    let (_, cert_buffer) = cert.get()?;
    let (_, private_key) = cert.get_private_key()?;

    let private_key = match private_key {
        PrivateKey::Ref(ref_) => PrivateKeyResponse::new("ref".to_string()).with_ref(ref_),
        PrivateKey::Key(buffer) => {
            PrivateKeyResponse::new("key".to_string()).with_bytes(base64::encode(&buffer))
        }
    };

    Ok(CertificateResponse::new(
        private_key,
        base64::encode(&cert_buffer),
        expiration.to_string(),
    ))
}

fn compute_validity(expiration: &str) -> Result<i64> {
    DateTime::parse_from_rfc3339(expiration)
        .map(|expiration| {
            expiration
                .with_timezone(&Utc)
                .signed_duration_since(Utc::now())
                .num_seconds()
        })
        .map_err(Error::from)
}
