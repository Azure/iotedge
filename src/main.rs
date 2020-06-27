mod constants;
mod ks;
mod message;
mod store;
mod util;

use std::convert::Infallible;
// use std::fs::remove_file;
use std::path::Path;

use hyper::Server;
use hyper::service::make_service_fn;
use hyperlocal::UnixServerExt;
use libc::{S_IRWXU, umask};

fn init() -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    unsafe {
        umask(!S_IRWXU);
    }

    Ok(())
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    init()?;

    let skt = Path::new("foo.sock");
    
    Server::bind_unix(skt)?
        .serve(make_service_fn(|_| async move {
            Ok::<_, Infallible>(message::MessageService)
        }))
        .await?;

    Ok(())
}
