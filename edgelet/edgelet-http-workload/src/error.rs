// Copyright (c) Microsoft. All rights reserved.

use std::fmt;

use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Response, StatusCode};
use log::error;
use workload::models::ErrorResponse;

use crate::IntoResponse;

#[derive(Clone, Debug, thiserror::Error)]
pub enum Error {
    #[error("Certificate has an invalid private key")]
    BadPrivateKey,

    #[error("{0}")]
    CertOperation(CertOperation),

    #[error("{0}")]
    EncryptionOperation(EncryptionOperation),

    #[error("Failed to get identity")]
    GetIdentity,

    #[error("Failed to load master encryption key")]
    LoadMasterEncKey,

    #[error("Invalid Identity type")]
    InvalidIdentityType,

    #[error("Request body is malformed")]
    MalformedRequestBody,

    #[error("The request parameter `{0}` is malformed")]
    MalformedRequestParameter(&'static str),

    #[error("The request is missing required parameter `{0}`")]
    MissingRequiredParameter(&'static str),

    #[error("Could not start workload service")]
    StartService,
}

impl IntoResponse for anyhow::Error {
    fn into_response(self) -> Response<Body> {
        let message = format!("{:?}", self);

        let status_code = if let Some(error) = self.downcast_ref() {
            match error {
                Error::MalformedRequestBody
                | Error::MalformedRequestParameter(_)
                | Error::MissingRequiredParameter(_) => StatusCode::BAD_REQUEST,
                _ => {
                    error!("Internal server error: {}", message);
                    StatusCode::INTERNAL_SERVER_ERROR
                }
            }
        } else {
            StatusCode::INTERNAL_SERVER_ERROR
        };

        // Per the RFC, status code NotModified should not have a body
        let body = if status_code == StatusCode::NOT_MODIFIED {
            String::new()
        } else {
            serde_json::to_string(&ErrorResponse::new(message))
                .expect("serialization of ErrorResponse failed.")
        };

        let mut response = Response::builder();
        response
            .status(status_code)
            .header(CONTENT_LENGTH, body.len().to_string().as_str());

        if !body.is_empty() {
            response.header(CONTENT_TYPE, "application/json");
        }

        response
            .body(body.into())
            .expect("response builder failure")
    }
}

#[derive(Clone, Copy, Debug)]
pub enum CertOperation {
    CreateIdentityCert,
    GetServerCert,
}

impl fmt::Display for CertOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            CertOperation::CreateIdentityCert => write!(f, "Could not create identity cert"),
            CertOperation::GetServerCert => write!(f, "Could not get server cert"),
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub enum EncryptionOperation {
    Decrypt,
    Encrypt,
    GetTrustBundle,
    Sign,
}

impl fmt::Display for EncryptionOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            EncryptionOperation::Decrypt => write!(f, "Could not decrypt"),
            EncryptionOperation::Encrypt => write!(f, "Could not encrypt"),
            EncryptionOperation::GetTrustBundle => write!(f, "Could not get trust bundle"),
            EncryptionOperation::Sign => write!(f, "Could not sign"),
        }
    }
}
