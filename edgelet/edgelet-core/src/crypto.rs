// Copyright (c) Microsoft. All rights reserved.

use bytes::Bytes;
use chrono::{DateTime, Utc};
use consistenttime::ct_u8_slice_eq;

use crate::certificate_properties::{CertificateIssuer, CertificateProperties};
use crate::error::Error;

/// ID of the device CA cert in certd and private key in keyd.
pub const AZIOT_EDGED_CA_ALIAS: &str = "aziot-edged-ca";

/// ID of the trust bundle cert in certd.
pub const TRUST_BUNDLE_ALIAS: &str = "aziot-edged-trust-bundle";

/// ID of the trust bundle cert in certd.
pub const MANIFEST_TRUST_BUNDLE_ALIAS: &str = "aziot-edged-manifest-trust-bundle";

pub trait Signature {
    fn as_bytes(&self) -> &[u8];
}

impl<T> Signature for T
where
    T: AsRef<[u8]>,
{
    fn as_bytes(&self) -> &[u8] {
        self.as_ref()
    }
}

#[derive(Debug)]
pub enum KeyBytes<T: AsRef<[u8]>> {
    Pem(T),
}

impl<T> Clone for KeyBytes<T>
where
    T: AsRef<[u8]> + Clone,
{
    fn clone(&self) -> Self {
        match *self {
            KeyBytes::Pem(ref val) => KeyBytes::Pem(val.clone()),
        }
    }
}

#[derive(Debug)]
pub enum PrivateKey<T: AsRef<[u8]>> {
    Ref(String),
    Key(KeyBytes<T>),
}

impl<T> Clone for PrivateKey<T>
where
    T: AsRef<[u8]> + Clone,
{
    fn clone(&self) -> Self {
        match *self {
            PrivateKey::Ref(ref sref) => PrivateKey::Ref(sref.clone()),
            PrivateKey::Key(ref val) => PrivateKey::Key(val.clone()),
        }
    }
}

pub trait GetIssuerAlias {
    fn get_issuer_alias(&self, issuer: CertificateIssuer) -> Result<String, Error>;
}

pub trait GetDeviceIdentityCertificate {
    type Certificate: Certificate;
    type Buffer: AsRef<[u8]>;

    fn get(&self) -> Result<Self::Certificate, Error>;
    fn sign_with_private_key(&self, data: &[u8]) -> Result<Self::Buffer, Error>;
}

pub trait CreateCertificate {
    type Certificate: Certificate;

    fn create_certificate(
        &self,
        properties: &CertificateProperties,
    ) -> Result<Self::Certificate, Error>;

    fn destroy_certificate(&self, alias: String) -> Result<(), Error>;

    fn get_certificate(&self, alias: String) -> Result<Self::Certificate, Error>;
}

pub trait Certificate {
    type Buffer: AsRef<[u8]>;
    type KeyBuffer: AsRef<[u8]>;

    fn pem(&self) -> Result<Self::Buffer, Error>;
    fn get_private_key(&self) -> Result<Option<PrivateKey<Self::KeyBuffer>>, Error>;
    fn get_valid_to(&self) -> Result<DateTime<Utc>, Error>;
    fn get_common_name(&self) -> Result<String, Error>;
}

#[derive(Debug)]
pub struct Digest {
    bytes: Bytes,
}

impl PartialEq for Digest {
    fn eq(&self, other: &Self) -> bool {
        ct_u8_slice_eq(self.bytes.as_ref(), other.bytes.as_ref())
    }
}

impl Signature for Digest {
    fn as_bytes(&self) -> &[u8] {
        self.bytes.as_ref()
    }
}

impl Digest {
    pub fn new(bytes: Bytes) -> Self {
        Digest { bytes }
    }
}
