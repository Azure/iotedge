mod constants;
mod message;
mod store;
mod util;

use std::fs::remove_file;
use std::path::Path;

use hyper::Server;
use hyper::service::{make_service_fn, service_fn};
use hyperlocal::UnixServerExt;
use libc::{S_IRWXU, umask};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    unsafe {
        umask(!S_IRWXU);
    }

    let skt = Path::new("foo.sock");
    Server::bind_unix(skt)?
        .serve(make_service_fn(|_| async {
            Ok::<_, hyper::Error>(service_fn(message::service))
        }))
        .await?;

    Ok(())
}
