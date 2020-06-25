use std::os::raw::c_char;
use std::path::Path;

use base64::encode;
use libsodium_sys as sodium;
use lmdb::Transaction;
// use zeroize::Zeroize;

pub struct Store {
    env: lmdb::Environment,
    db: lmdb::Database
}

impl Store {
    pub fn new(path: &Path) -> Result<Self, lmdb::Error> {
        let env = lmdb::Environment::new()
            .open(path)?;
        let db = env.create_db(None, lmdb::DatabaseFlags::empty())?;
        Ok(Store { env: env, db: db })
    }

    pub fn get_secret<'a>(&self, id: String) -> Result<String, lmdb::Error> {
        let key = derive_key(&id);

        let txn = self.env .begin_ro_txn()?;
        let val = Vec::from(txn.get(self.db, &key)?);
        txn.commit()?;

        Ok(encode(val))
    }

    pub fn set_secret<'a>(&self, id: String, value: String) -> Result<(), lmdb::Error> {
        let key = derive_key(&id);

        Ok(())
    }
}

fn derive_key(id: &String) -> Vec<u8> {
    let mut salt_buf = vec![0u8; sodium::crypto_hash_sha256_BYTES as usize];
    unsafe {
        sodium::crypto_hash_sha256(
            salt_buf.as_mut_ptr(),
            id.as_ptr(),
            id.len() as u64
        );
    }

    let mut key_buf = vec![0u8; sodium::crypto_pwhash_STRBYTES as usize];
    unsafe {
        sodium::crypto_pwhash(
            key_buf.as_mut_ptr(),
            key_buf.len() as u64,
            id.as_ptr() as *const c_char,
            id.len() as u64,
            salt_buf.as_ptr(),
            sodium::crypto_pwhash_OPSLIMIT_MODERATE as u64,
            sodium::crypto_pwhash_MEMLIMIT_MODERATE as usize,
            sodium::crypto_pwhash_ALG_DEFAULT as i32
        );
    }
    key_buf
}
