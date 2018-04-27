// Copyright (c) Microsoft. All rights reserved.

extern crate edgelet_core;
#[macro_use]
extern crate failure;
extern crate hsm;

use std::sync::{Arc, RwLock};

use edgelet_core::Error as CoreError;
use edgelet_core::crypto::{KeyStore as CoreKeyStore, Sign, SignatureAlgorithm};
use hsm::{ManageTpmKeys, SignWithTpm, Tpm, TpmDigest};

mod error;

pub use error::{Error, ErrorKind};

/// Represents a key which can sign data.
pub struct TpmKey {
    tpm: Arc<RwLock<Tpm>>,
    identity: Option<String>,
}

/// The TPM Key Store.
/// Activate a private key, and then you can use that key to sign data.
#[derive(Clone, Default)]
pub struct TpmKeyStore {
    tpm: Arc<RwLock<Tpm>>,
}

impl TpmKeyStore {
    pub fn new(tpm: Tpm) -> Result<TpmKeyStore, Error> {
        Ok(TpmKeyStore {
            tpm: Arc::new(RwLock::new(tpm)),
        })
    }

    /// Activate and store a private key in the TPM.
    pub fn activate_key(&self, key_value: &str) -> Result<(), Error> {
        self.tpm
            .read()
            .expect("Read lock on KeyStore TPM failed")
            .activate_identity_key(key_value.as_bytes())
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
        if identity.len() == 0 {
            Err(Error::from(ErrorKind::EmptyStrings))?;
        }
        Ok(TpmKey {
            tpm: Arc::clone(&self.tpm),
            identity: Some(identity.to_string()),
        })
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
