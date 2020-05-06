// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use bytes::Bytes;
use failure::Fail;

use edgelet_core::crypto::{
    Activate, GetHsmVersion as CoreGetHsmVersion, KeyIdentity, KeyStore as CoreKeyStore, Sign,
    SignatureAlgorithm,
};
use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind};
use hsm::{ManageTpmKeys, SignWithTpm, Tpm, TpmDigest};

pub use crate::error::{Error, ErrorKind};
use crate::HsmLock;

const ROOT_KEY_NAME: &str = "primary";

/// Represents a key which can sign data.
#[derive(Clone, Debug)]
pub struct TpmKey {
    tpm: Arc<Tpm>,
    identity: KeyIdentity,
    key_name: String,
    hsm_lock: Arc<HsmLock>,
}

// hsm::Tpm is Send and !Sync. However TpmKey can be Sync since all access to TpmKey::tpm
// is controlled by the methods of TpmKey, which all lock TpmKey::hsm_lock first.
//
// For the same reason, TpmKey also needs an explicit Send impl
// since Arc<T>: Send requires T: Send + Sync.
unsafe impl Send for TpmKey {}
unsafe impl Sync for TpmKey {}

/// The TPM Key Store.
/// Activate a private key, and then you can use that key to sign data.
#[derive(Clone)]
pub struct TpmKeyStore {
    tpm: Arc<Tpm>,
    hsm_lock: Arc<HsmLock>,
}

// hsm::Tpm is Send and !Sync. However TpmKeyStore can be Sync since all access to TpmKeyStore::tpm
// is controlled by the methods of TpmKeyStore, which all lock TpmKeyStore::hsm_lock first.
//
// For the same reason, TpmKeyStore also needs an explicit Send impl
// since Arc<T>: Send requires T: Send + Sync.
unsafe impl Send for TpmKeyStore {}
unsafe impl Sync for TpmKeyStore {}

impl TpmKeyStore {
    pub fn new(hsm_lock: Arc<HsmLock>) -> Result<Self, Error> {
        let hsm = Tpm::new()?;
        TpmKeyStore::from_hsm(hsm, hsm_lock)
    }

    pub fn from_hsm(tpm: Tpm, hsm_lock: Arc<HsmLock>) -> Result<Self, Error> {
        Ok(TpmKeyStore {
            tpm: Arc::new(tpm),
            hsm_lock,
        })
    }

    /// Activate and store a private key in the TPM.
    pub fn activate_key(&self, key_value: &Bytes) -> Result<(), Error> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.tpm.activate_identity_key(key_value)?;
        Ok(())
    }

    /// Get a `TpmKey` which will sign data.
    pub fn get_active_key(&self) -> Result<TpmKey, Error> {
        Ok(TpmKey {
            tpm: Arc::clone(&self.tpm),
            identity: KeyIdentity::Device,
            key_name: ROOT_KEY_NAME.to_string(),
            hsm_lock: self.hsm_lock.clone(),
        })
    }
}

impl CoreGetHsmVersion for TpmKeyStore {
    fn get_version(&self) -> Result<String, CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.tpm
            .get_version()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::HsmVersion)))
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
                    hsm_lock: self.hsm_lock.clone(),
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
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        match self.identity {
            KeyIdentity::Device => self
                .tpm
                .sign_with_identity(data)
                .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
                .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore))),
            KeyIdentity::Module(ref _m) => self
                .tpm
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
