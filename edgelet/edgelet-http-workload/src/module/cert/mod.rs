// Copyright (c) Microsoft. All rights reserved.

pub(crate) mod identity;
pub(crate) mod server;

#[derive(Debug, serde::Serialize)]
#[serde(tag = "type")]
pub(crate) enum PrivateKey {
    #[serde(rename = "ref")]
    Reference {
        #[serde(rename = "ref")]
        reference: String,
    },
    Bytes {
        bytes: String,
    },
}

#[derive(Debug, serde::Serialize)]
pub(crate) struct CertificateResponse {
    private_key: PrivateKey,
    certificate: String,
    expiration: String,
}
