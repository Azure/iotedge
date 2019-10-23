use std::net::{IpAddr, Ipv4Addr, SocketAddr};
use std::path::PathBuf;

use futures::future::try_join;
use log::*;
use tokio::net::{TcpListener, TcpStream, UnixStream};
use tokio::prelude::*;
use tokio::sync::oneshot;

/// Proxy data between a local socket and a local TCP server
///
/// Adapted from https://github.com/tokio-rs/tokio/blob/master/examples/proxy.rs
pub async fn server(
    sock: PathBuf,
    port: u16,
    ready: oneshot::Sender<()>,
) -> Result<(), failure::Error> {
    // wait for client to connect to tcp server
    let addr = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), port);
    let mut incoming = TcpListener::bind(&addr).await?.incoming();

    ready.send(()).unwrap();

    while let Some(Ok(inbound)) = incoming.next().await {
        let client_addr = inbound.peer_addr()?;
        if addr.ip() != client_addr.ip() {
            failure::bail!(
                "non-local client ({:?}) attempted to connect to proxy",
                client_addr
            );
        }

        let sock = sock.clone();
        tokio::spawn(async move {
            if let Err(e) = transfer(inbound, sock).await {
                error!("connection error: {}", e);
            }
        });
    }
    Ok(())
}

async fn transfer(mut inbound: TcpStream, sock: PathBuf) -> Result<(), failure::Error> {
    let mut outbound = UnixStream::connect(sock).await?;

    let (mut ri, mut wi) = inbound.split();
    let (mut ro, mut wo) = outbound.split();

    let client_to_server = ri.copy(&mut wo);
    let server_to_client = ro.copy(&mut wi);

    try_join(client_to_server, server_to_client).await?;

    Ok(())
}
