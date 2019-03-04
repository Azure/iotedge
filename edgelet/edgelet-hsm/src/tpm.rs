// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use bytes::Bytes;
use failure::Fail;

use edgelet_core::crypto::{
    Activate, KeyIdentity, KeyStore as CoreKeyStore, Sign, SignatureAlgorithm,
};
use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind};
use hsm::{ManageTpmKeys, SignWithTpm, Tpm, TpmDigest};

pub use crate::error::{Error, ErrorKind};

const ROOT_KEY_NAME: &str = "primary";

/// Represents a key which can sign data.
#[derive(Clone, Debug)]
pub struct TpmKey {
    tpm: Arc<Mutex<Tpm>>,
    identity: KeyIdentity,
    key_name: String,
}

/// The TPM Key Store.
/// Activate a private key, and then you can use that key to sign data.
#[derive(Clone)]
pub struct TpmKeyStore {
    tpm: Arc<Mutex<Tpm>>,
}

impl TpmKeyStore {
    pub fn new() -> Result<Self, Error> {
        let hsm = Tpm::new()?;
        TpmKeyStore::from_hsm(hsm)
    }

    pub fn from_hsm(tpm: Tpm) -> Result<Self, Error> {
        Ok(TpmKeyStore {
            tpm: Arc::new(Mutex::new(tpm)),
        })
    }

    /// Activate and store a private key in the TPM.
    pub fn activate_key(&self, key_value: &Bytes) -> Result<(), Error> {
        self.tpm
            .lock()
            .expect("Lock on KeyStore TPM failed")
            .activate_identity_key(key_value)?;
        Ok(())
    }

    /// Get a TpmKey which will sign data.
    pub fn get_active_key(&self) -> Result<TpmKey, Error> {
        Ok(TpmKey {
            tpm: Arc::clone(&self.tpm),
            identity: KeyIdentity::Device,
            key_name: ROOT_KEY_NAME.to_string(),
        })
    }
}

impl CoreKeyStore for TpmKeyStore {
    type Key = TpmKey;

    /// Get a TPM Key which will derive and sign data.
    fn get(&self, identity: &KeyIdentity, key_name: &str) -> Result<Self::Key, CoreError> {
        match *identity {
            KeyIdentity::Device => self
                .get_active_key()
                .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore))),
            KeyIdentity::Module(ref m) => {
                if key_name.is_empty() || m.is_empty() {
                    Err(ErrorKind::EmptyStrings)
                        .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
                        .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))?;
                }
                Ok(TpmKey {
                    tpm: Arc::clone(&self.tpm),
                    identity: identity.clone(),
                    key_name: key_name.to_string(),
                })
            }
        }
    }
}

impl Activate for TpmKeyStore {
    type Key = TpmKey;

    fn activate_identity_key<B: AsRef<[u8]>>(
        &mut self,
        identity: KeyIdentity,
        _key_name: String,
        key: B,
    ) -> Result<(), CoreError> {
        if identity != KeyIdentity::Device {
            Err(ErrorKind::NoModuleActivation)
                .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
                .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))?;
        }
        self.activate_key(&Bytes::from(key.as_ref()))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
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
        match self.identity {
            KeyIdentity::Device => self
                .tpm
                .lock()
                .expect("Lock failed")
                .sign_with_identity(data)
                .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
                .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore))),
            KeyIdentity::Module(ref _m) => self
                .tpm
                .lock()
                .expect("Lock failed")
                .derive_and_sign_with_identity(
                    data,
                    format!(
                        "{}{}",
                        match self.identity {
                            KeyIdentity::Device => "",
                            KeyIdentity::Module(ref m) => m,
                        },
                        self.key_name
                    )
                    .as_bytes(),
                )
                .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
                .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore))),
        }
    }
}
