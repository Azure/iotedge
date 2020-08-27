mod backends;
mod config;
mod constants;
mod routes;
mod store;
mod util;

use crate::backends::rocksdb;
use crate::store::{Store, StoreBackend as _};

use std::error::Error as StdError;
use std::fs::remove_file;
use std::io::ErrorKind;
use std::path::Path;
use std::sync::Arc;

use hyper::{Error as HyperError, Response, Server};
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
    let skt = Path::new(constants::SOCKET_NAME);

    init(skt);

    let store = {
        let conf = config::load(Path::new("store.toml"));
        let backend = rocksdb::RocksDBBackend::new()?;
        Arc::new(Store::new(backend, conf))
    };
    
    Server::bind_unix(skt)?
        .serve(make_service_fn(|_conn: &UnixStream| {
            let store = store.to_owned();
            async {
                <Result<_, HyperError>>::Ok(service_fn(move |req| {
                    let store = store.to_owned();
                    async move {
                        routes::dispatch(&store, req).await
                            .or_else(|e| {
                                println!("ERROR: {:?}", e);
                                Response::builder().status(500).body(format!("{:?}", e).into())
                            })
                    }
                }))
            }
        }))
        .await?;

    Ok(())
}
