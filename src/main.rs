mod backends;
mod config;
mod constants;
mod error;
mod routes;
mod store;

use crate::error::ErrorKind;
use crate::backends::rocksdb;
use crate::store::Store;

use std::error::Error as StdError;
use std::io;
use std::fs::remove_file;
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
        Err(e) if e.kind() != io::ErrorKind::NotFound =>
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
    let conf = config::load(Path::new(constants::CONFIGURATION_FILE));
    let skt = Path::new(constants::LISTEN_SOCKET);

    init(skt);

    let store = {
        let backend = rocksdb::RocksDBBackend::new(&conf.local.storage_location).unwrap();
        Arc::new(Store::new(backend, conf))
    };

    Server::bind_unix(skt)?
        .serve(make_service_fn(|conn: &UnixStream| {
            let store = store.to_owned();
            let creds = conn.peer_cred().unwrap().clone();
            async move {
                <Result<_, HyperError>>::Ok(service_fn(move |mut req| {
                    let store = store.to_owned();
                    req.extensions_mut().insert(creds.clone());
                    async move {
                        routes::dispatch(&store, req).await
                            .or_else(|e| {
                                println!("ERROR: {:?}", e);
                                match e.kind() {
                                    ErrorKind::CorruptData => Response::builder().status(400).body(Body::empty()),
                                    ErrorKind::Unauthorized => Response::builder().status(403).body(Body::empty()),
                                    ErrorKind::NotFound => Response::builder().status(404).body(Body::empty()),
                                    e => Response::builder().status(500).body(format!("{}", e).into())
                                }
                            })
                    }
                }))
            }
        }))
        .await?;

    Ok(())
}
