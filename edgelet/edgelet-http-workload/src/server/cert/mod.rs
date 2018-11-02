// Copyright (c) Microsoft. All rights reserved.

use std::cmp;
use chrono::{DateTime, Utc};
use edgelet_core::{
    Certificate, KeyBytes, PrivateKey, CertificateProperties,CreateCertificate
};
use error::{Error, ErrorKind, Result};
use workload::models::{
    CertificateResponse, PrivateKey as PrivateKeyResponse,
};
use serde_json;
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Response, StatusCode};
use hyper::{Body};

mod identity;
mod server;

pub use self::identity::IdentityCertHandler;
pub use self::server::ServerCertHandler;

fn cert_to_response<T: Certificate>(cert: &T) -> Result<CertificateResponse> {
    let cert_buffer = cert.pem()?;
    let expiration = cert.get_valid_to()?;

    let private_key = match cert.get_private_key()? {
        Some(PrivateKey::Ref(ref_)) => PrivateKeyResponse::new("ref".to_string()).with_ref(ref_),
        Some(PrivateKey::Key(KeyBytes::Pem(buffer))) => PrivateKeyResponse::new("key".to_string())
            .with_bytes(String::from_utf8_lossy(buffer.as_ref()).to_string()),
        None => Err(ErrorKind::BadPrivateKey)?,
    };

    Ok(CertificateResponse::new(
        private_key,
        String::from_utf8_lossy(cert_buffer.as_ref()).to_string(),
        expiration.to_rfc3339(),
    ))
}

fn compute_validity(expiration: &str, max_duration_sec: i64) -> Result<i64> {
    ensure_not_empty!(expiration);
    DateTime::parse_from_rfc3339(expiration)
        .map(|expiration| {
            let secs = expiration
                .with_timezone(&Utc)
                .signed_duration_since(Utc::now())
                .num_seconds();
            cmp::min(secs, max_duration_sec)
        }).map_err(Error::from)
}

fn refresh_cert<T: CreateCertificate> (hsm: &T, alias: String, props: &CertificateProperties) -> Result<Response<Body>> {
    hsm.destroy_certificate(alias)
        .map_err(Error::from)?;

    hsm.create_certificate(props)
        .map_err(Error::from)
        .and_then(|cert| {
            let cert = cert_to_response(&cert)?;
            let body = serde_json::to_string(&cert)?;
            Response::builder()
                .status(StatusCode::CREATED)
                .header(CONTENT_TYPE, "application/json")
                .header(CONTENT_LENGTH, body.len().to_string().as_str())
                .body(body.into())
                .map_err(From::from)
        })
}