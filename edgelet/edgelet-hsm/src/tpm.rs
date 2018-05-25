// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, RwLock};

use bytes::Bytes;

use edgelet_core::Error as CoreError;
use edgelet_core::crypto::{Activate, KeyStore as CoreKeyStore, Sign, SignatureAlgorithm};
use hsm::{ManageTpmKeys, SignWithTpm, Tpm, TpmDigest};

pub use error::{Error, ErrorKind};

/// Represents a key which can sign data.
#[derive(Clone)]
pub struct TpmKey {
    tpm: Arc<RwLock<Tpm>>,
    identity: Option<String>,
}

/// The TPM Key Store.
/// Activate a private key, and then you can use that key to sign data.
#[derive(Clone)]
pub struct TpmKeyStore {
    tpm: Arc<RwLock<Tpm>>,
}

impl TpmKeyStore {
    pub fn new() -> Result<TpmKeyStore, Error> {
        let hsm = Tpm::new()?;
        TpmKeyStore::from_hsm(hsm)
    }

    pub fn from_hsm(tpm: Tpm) -> Result<TpmKeyStore, Error> {
        Ok(TpmKeyStore {
            tpm: Arc::new(RwLock::new(tpm)),
        })
    }

    /// Activate and store a private key in the TPM.
    pub fn activate_key(&self, key_value: Bytes) -> Result<(), Error> {
        self.tpm
            .read()
            .expect("Read lock on KeyStore TPM failed")
            .activate_identity_key(&key_value)
            .map_err(Error::from)?;
        Ok(())
    }

    /// Get a TpmKey which will sign data.
    pub fn get_active_key(&self) -> Result<TpmKey, Error> {
        Ok(TpmKey {
            tpm: Arc::clone(&self.tpm),
            identity: None,
        })
    }
}

impl CoreKeyStore for TpmKeyStore {
    type Key = TpmKey;

    /// Get a TPM Key which will derive and sign data.
    fn get(&self, identity: &str, _key_name: &str) -> Result<Self::Key, CoreError> {
        if identity.is_empty() {
            Err(Error::from(ErrorKind::EmptyStrings))?;
        }
        Ok(TpmKey {
            tpm: Arc::clone(&self.tpm),
            identity: Some(identity.to_string()),
        })
    }
}

impl Activate for TpmKeyStore {
    type Key = TpmKey;

    fn activate_identity_key<B: AsRef<[u8]>>(
        &mut self,
        _identity: String,
        _key_name: String,
        key: B,
    ) -> Result<(), CoreError> {
        self.activate_key(Bytes::from(key.as_ref()))
            .map_err(CoreError::from)
    }
}

impl Sign for TpmKey {
    type Signature = TpmDigest;

    /// Sign data with this key.
    /// If an identity was given, we will derive a new key from the identity and sign the data.
    /// If an identity was not given, we will sign the data with the stored key.
    fn sign(
        &self,
        _signature_algorithm: SignatureAlgorithm,
        data: &[u8],
    ) -> Result<Self::Signature, CoreError> {
        if let Some(ref id) = self.identity.as_ref() {
            self.tpm
                .read()
                .expect("Read lock failed")
                .derive_and_sign_with_identity(data, id.as_bytes())
                .map_err(Error::from)
                .map_err(CoreError::from)
        } else {
            self.tpm
                .read()
                .expect("Read lock failed")
                .sign_with_identity(data)
                .map_err(Error::from)
                .map_err(CoreError::from)
        }
    }
}
