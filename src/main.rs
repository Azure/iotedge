mod backends;
mod config;
mod constants;
mod ks;
mod routes;
mod store;
mod util;

use store::{Store, StoreBackend};

use std::error::Error as StdError;
use std::fs::remove_file;
use std::io::ErrorKind;
use std::path::Path;
use std::sync::Arc;

use hyper::{Error as HyperError, Server};
use hyper::service::{make_service_fn, service_fn};
use hyperlocal::UnixServerExt;
use libc::{S_IRWXU, umask};
use ring::rand::{generate, SystemRandom};

fn init(path: &Path) {
    unsafe {
        umask(!S_IRWXU);
    }

    
    match remove_file(path) {
        Err(e) if e.kind() != ErrorKind::NotFound =>
            panic!("COULD NOT REMOVE EXISTING SOCKET"),
        _ => ()
    }

    // NOTE: coerce PRNG initialization to reduce latency later
    //       https://briansmith.org/rustdoc/ring/rand/struct.SystemRandom.html
    if let Err(_) = generate::<[u8; 8]>(&SystemRandom::new()) {
        panic!("FAILED TO INITIALIZE PRNG");
    }
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn StdError + Send + Sync>> {
    let skt = Path::new(constants::SOCKET_NAME);

    init(skt);

    let store = {
        // let conf = config::load(Path::new("foo.toml"));
        let backend = crate::backends::rocksdb::RocksDBBackend::new()?;
        Arc::new(Store::new(backend/*, conf*/))
    };
    
    Server::bind_unix(skt)?
        .serve(make_service_fn(|_| {
            let store = store.to_owned();
            async {
                <Result<_, HyperError>>::Ok(service_fn(move |req| {
                    routes::dispatch(store.to_owned(), req)
                }))
            }
        }))
        .await?;

    Ok(())
}
