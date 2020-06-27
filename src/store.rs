use crate::ks;
use crate::ks::{KeyHandle, Text};
use crate::util::BoxedResult;

use std::future::Future;
use std::path::Path;
use std::pin::Pin;

use base64::encode;
use zeroize::Zeroize;

pub trait StoreBackend {
    fn initialize();
    fn write_record(id: &str, record: Record);
    fn update_record(id: &str, record: Record);
    fn read_record(id: &str);
    fn delete_record(id: &str);
}

pub trait Store: StoreBackend {
    // NOTE: can remove Pin<Box<...>> if async traits are added to Rust
    //       cf. https://docs.rs/crate/async-trait
    fn get_secret(&self, id: &str) -> Pin<Box<dyn Future<Output = BoxedResult<String>>>> {
        Box::pin(async {
            Ok(String::from("FOO"))
        })
    }
}

#[derive(Zeroize)]
#[zeroize(drop)]
pub struct Record {
    ciphertext: String,
    iv: String,
    aad: String
}
