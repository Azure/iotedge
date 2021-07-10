// Copyright (c) Microsoft. All rights reserved.

pub(crate) mod identity;
pub(crate) mod server;

#[derive(Debug, serde::Serialize)]
pub(crate) struct CertificateResponse {
    certificate: String,
    expiration: String,
}
