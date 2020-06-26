mod constants;
mod ks;
mod message;
mod store;
mod util;

use crate::constants::SERVER_DIR;
use crate::store::Store;

// use std::fs::remove_file;
use std::path::Path;

use hyper::Server;
use hyper::service::make_service_fn;
use hyperlocal::UnixServerExt;
use libc::{S_IRWXU, umask};
use libsodium_sys::sodium_init;
// use zeroize::Zeroize;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    unsafe {
        umask(!S_IRWXU);
        sodium_init();
    }

    let skt = Path::new("foo.sock");
    let store = Store::new(Path::new(SERVER_DIR));
    
    Server::bind_unix(skt)?
        .serve(make_service_fn(|_| async move {
            message::MessageService::new()
        }))
        .await?;

    Ok(())
}
