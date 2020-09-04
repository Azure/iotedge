mod backends;
mod config;
mod constants;
mod error;
mod routes;
mod store;
mod unix;
mod util;

use crate::backends::rocksdb;

use std::error::Error as StdError;
use std::io;
use std::fs::remove_file;
use std::path::Path;

use scopeguard::defer;
use hyper::Server;
use hyperlocal::UnixServerExt;
use ring::rand::{generate, SystemRandom};

fn init(path: &Path) {
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
    defer!(remove_file(skt).unwrap());

    let routes = unix::with_umask(0o022, move || {
        let backend = rocksdb::RocksDBBackend::new(&conf.local.storage_location).unwrap();
        routes::connect(backend, conf)
    });
    let auth_service = util::InjectUnixCredentials::new(routes);

    unix::with_umask(0o111, || Server::bind_unix(skt))?
        .serve(auth_service)
        .await?;

    Ok(())
}
