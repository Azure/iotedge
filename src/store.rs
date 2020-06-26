use crate::constants::HSM_SERVER;
use crate::ks::{KSClient, KeyHandle, Text};
use crate::util::BoxedResult;

use std::os::raw::c_char;
use std::path::Path;

use base64::encode;
use libsodium_sys as sodium;
use rusqlite::{params, Connection, Error as SQLiteError};
// use zeroize::Zeroize;

pub struct Store<'a> {
    db: Connection,
    ksc: KSClient<'a>
}

impl<'a> Store<'a> {
    pub fn new(path: &Path) -> Result<Self, SQLiteError> {
        let db = Connection::open(path.join("store.db3"))?;
        db.execute(
            "CREATE TABLE IF NOT EXISTS secrets (
                id      BLOB PRIMARY KEY,
                value   TEXT,
                iv      TEXT,
                aad     TEXT
            ) WITHOUT ROWID",
            params![]
        )?;
        let client = KSClient::new(HSM_SERVER);
        Ok(Store { db: db, ksc: client })
    }

    pub async fn get_secret(&self, id: &str) -> BoxedResult<String> {
        let sha = compute_sha(id);
        let secret_id = derive_key(id, &sha);

        let id_param = params![secret_id];
        let mut stmt = self.db.prepare("SELECT * FROM keys WHERE id = ?")?;
        let mut query = stmt.query(id_param)?;

        if let Some(row) = query.next()? {
            let val: String = row.get(1)?;
            let iv: String = row.get(2)?;
            let aad: String = row.get(3)?;

            let KeyHandle(sym_key) = self.ksc
                .get_key(&encode(sha))
                .await?;

            let text = self.ksc
                .decrypt(
                    sym_key,
                    &val,
                    &iv,
                    &aad
                )
                .await?;

            match text {
                Text::Plaintext(ptext) => Ok(ptext.to_string()),
                _ => panic!("KEY SERVER RETURNED CIPHERTEXT")
            }
        }
        else {
            Err(Box::new(SQLiteError::QueryReturnedNoRows))
        }
    }

    pub async fn set_secret(&self, id: String, value: String) -> BoxedResult<()> {
        // let sha = compute_sha(id);
        // let key = derive_key(id, sha);

        Ok(())
    }
}

fn compute_sha(id: &str) -> Vec<u8> {
    let mut buf = vec![0u8; sodium::crypto_hash_sha256_BYTES as usize];
    unsafe {
        sodium::crypto_hash_sha256(
            buf.as_mut_ptr(),
            id.as_ptr(),
            id.len() as u64
        );
    }
    buf
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
