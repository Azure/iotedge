use crate::constants::HSM_SERVER;
use crate::ks::KSClient;
use crate::util::BoxedResult;

use std::os::raw::c_char;
use std::path::Path;

use base64::encode;
use libsodium_sys as sodium;
use lmdb::Transaction;
// use zeroize::Zeroize;

pub struct Store<'a> {
    env: lmdb::Environment,
    db: lmdb::Database,
    ksc: KSClient<'a>
}

impl<'a> Store<'a> {
    pub fn new(path: &Path) -> Result<Self, lmdb::Error> {
        let env = lmdb::Environment::new()
            .open(path)?;
        let db = env.create_db(None, lmdb::DatabaseFlags::empty())?;
        let client = KSClient::new(HSM_SERVER);
        Ok(Store { env: env, db: db, ksc: client })
    }

    pub async fn get_secret(&self, id: &str) -> BoxedResult<String> {
        let sha = compute_sha(id);
        let key = derive_key(id, sha);

        let txn = self.env .begin_ro_txn()?;
        let val = Vec::from(txn.get(self.db, &key)?);
        txn.commit()?;

        let enc_key = self.ksc
            .get_key(&encode(sha))
            .await?;

        Ok(encode(val))
    }

    pub async fn set_secret(&self, id: String, value: String) -> BoxedResult<()> {
        // let sha = compute_sha(id);
        // let key = derive_key(id, sha);

        Ok(())
    }
}

fn compute_sha(id: &str) -> &Vec<u8> {
    let mut buf = vec![0u8; sodium::crypto_hash_sha256_BYTES as usize];
    unsafe {
        sodium::crypto_hash_sha256(
            buf.as_mut_ptr(),
            id.as_ptr(),
            id.len() as u64
        );
    }
    &buf
}

fn derive_key(id: &str, salt: &Vec<u8>) -> Vec<u8> {
    let mut key_buf = vec![0u8; sodium::crypto_pwhash_STRBYTES as usize];
    unsafe {
        sodium::crypto_pwhash(
            key_buf.as_mut_ptr(),
            key_buf.len() as u64,
            id.as_ptr() as *const c_char,
            id.len() as u64,
            salt.as_ptr(),
            sodium::crypto_pwhash_OPSLIMIT_MODERATE as u64,
            sodium::crypto_pwhash_MEMLIMIT_MODERATE as usize,
            sodium::crypto_pwhash_ALG_DEFAULT as i32
        );
    }
    key_buf
}
