// Copyright (c) Microsoft. All rights reserved.

use std::cmp;

use chrono::{DateTime, Utc};
use failure::{Fail, ResultExt};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Response, StatusCode};
use serde_json;

use edgelet_core::{Certificate, CertificateProperties, CreateCertificate, KeyBytes, PrivateKey};
use edgelet_utils::ensure_not_empty_with_context;
use workload::models::{CertificateResponse, PrivateKey as PrivateKeyResponse};

use crate::error::{Error, ErrorKind, Result};

mod identity;
mod server;

pub use self::identity::IdentityCertHandler;
pub use self::server::ServerCertHandler;

fn cert_to_response<T: Certificate>(cert: &T, context: ErrorKind) -> Result<CertificateResponse> {
    let cert_buffer = match cert.pem() {
        Ok(cert_buffer) => cert_buffer,
        Err(err) => return Err(Error::from(err.context(context))),
    };

    let expiration = match cert.get_valid_to() {
        Ok(expiration) => expiration,
        Err(err) => return Err(Error::from(err.context(context))),
    };

    let private_key = match cert.get_private_key() {
        Ok(Some(PrivateKey::Ref(ref_))) => {
            PrivateKeyResponse::new("ref".to_string()).with_ref(ref_)
        }
        Ok(Some(PrivateKey::Key(KeyBytes::Pem(buffer)))) => {
            PrivateKeyResponse::new("key".to_string())
                .with_bytes(String::from_utf8_lossy(buffer.as_ref()).to_string())
        }
        Ok(None) => return Err(ErrorKind::BadPrivateKey.into()),
        Err(err) => return Err(Error::from(err.context(context))),
    };

    Ok(CertificateResponse::new(
        private_key,
        String::from_utf8_lossy(cert_buffer.as_ref()).to_string(),
        expiration.to_rfc3339(),
    ))
}

fn compute_validity(expiration: &str, max_duration_sec: i64, context: ErrorKind) -> Result<i64> {
    ensure_not_empty_with_context(expiration, || context.clone())?;

    let expiration = DateTime::parse_from_rfc3339(expiration).context(context)?;

    let secs = expiration
        .with_timezone(&Utc)
        .signed_duration_since(Utc::now())
        .num_seconds();

    Ok(cmp::min(secs, max_duration_sec))
}

fn refresh_cert<T: CreateCertificate>(
    hsm: &T,
    alias: String,
    props: &CertificateProperties,
    context: ErrorKind,
) -> Result<Response<Body>> {
    if let Err(err) = hsm.destroy_certificate(alias) {
        return Err(Error::from(err.context(context)));
    };

    let cert = match hsm.create_certificate(props) {
        Ok(cert) => cert,
        Err(err) => return Err(Error::from(err.context(context))),
    };

    let cert = cert_to_response(&cert, context.clone())?;

    let body = match serde_json::to_string(&cert) {
        Ok(body) => body,
        Err(err) => return Err(Error::from(err.context(context))),
    };

    let response = Response::builder()
        .status(StatusCode::CREATED)
        .header(CONTENT_TYPE, "application/json")
        .header(CONTENT_LENGTH, body.len().to_string().as_str())
        .body(body.into())
        .context(context)?;

    Ok(response)
}
