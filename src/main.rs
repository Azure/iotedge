mod backends;
mod config;
mod constants;
mod error;
mod server;
mod store;
mod util;

use crate::backends::rocksdb::RocksDBBackend;
use crate::constants::{CONFIGURATION_FILE, LISTEN_SOCKET};
use crate::store::{Store, StoreBackend as _};

use std::error::Error as StdError;
use std::fs::remove_file;
use std::io::ErrorKind;
use std::path::Path;
use std::sync::Arc;

use hyper::{Body, Error as HyperError, Response, Server};
use hyper::service::{make_service_fn, service_fn};
use hyperlocal::UnixServerExt;
use libc::{S_IRWXU, umask};
use ring::rand::{generate, SystemRandom};
use tokio::net::UnixStream;

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
    let conf = config::load(Path::new(CONFIGURATION_FILE));
    let skt = Path::new(LISTEN_SOCKET);

    init(skt);

    let store = {
        let backend = RocksDBBackend::new().unwrap();
        Arc::new(Store::new(backend, conf.credentials))
    };
    
    Server::bind_unix(skt)?
        .serve(make_service_fn(|conn: &UnixStream| {
            let store = store.to_owned();
            let peer_cred = conn.peer_cred().unwrap();
            async {
                <Result<_, HyperError>>::Ok(service_fn(move |req| {
                    let store = store.to_owned();
                    async move {
                        server::dispatch(&store, req).await
                            .or_else(|e| Response::builder().status(500).body(Body::from(format!("{:?}", e))))
                    }
                }))
            }
        }))
        .await?;

    Ok(())
}
