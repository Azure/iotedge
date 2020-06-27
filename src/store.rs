use crate::ks;
use crate::ks::{KeyHandle, Text};
use crate::util::BoxedResult;

// use std::future::{ready, Future};
use std::pin::Pin;

use base64::encode;
// WARN: switch to std::future when `ready` becomes stable
use futures::future::{ready, Future};
use zeroize::Zeroize;

pub trait StoreBackend {
    // NOTE: lack of &self is intentional
    fn initialize<'a>() -> BoxedResult<'a, ()>;

    fn write_record<'a>(&self, id: String, record: Record) -> BoxedResult<'a, ()>;
    fn update_record<'a>(&self, id: String, record: Record) -> BoxedResult<'a, ()>;
    fn read_record<'a>(&self, id: String) -> BoxedResult<'a, Record>;
    fn delete_record<'a>(&self, id: String) -> BoxedResult<'a, ()>;
}

// NOTE: not public since high-level functions should be invariant over
//       backend implementation
trait Store: StoreBackend {
    // NOTE: can remove Pin<Box<...>> if async traits are added to Rust
    //       cf. https://docs.rs/crate/async-trait
    fn get_secret<'a>(&self, id: String) -> Pin<Box<dyn Future<Output = BoxedResult<'a, String>>>> {
        let record = match self.read_record(id) {
            Ok(record) => record,
            Err(e) => return Box::pin(ready(<BoxedResult<String>>::Err(e)))
        };

        Box::pin(async move {
            let KeyHandle(key) = ks::get_key(id).await?;
            let ptext = match ks::decrypt(key, record.ciphertext, record.iv, record.aad).await? {
                Text::Plaintext(ptext) => ptext,
                _ => panic!("KEY SERVICE API CHANGED")
            };

            Ok(ptext)
        })
    }
}

#[derive(Zeroize)]
#[zeroize(drop)]
pub struct Record {
    pub ciphertext: String,
    pub iv: String,
    pub aad: String
}
