use std::path::PathBuf;

use log::*;
use tokio::sync::oneshot;

use containerd_grpc::services::version::v1::client::VersionClient;

mod sock_to_tcp_proxy;

#[tokio::main]
async fn main() {
    if let Err(fail) = true_main().await {
        println!("{}", fail);
        for cause in fail.iter_causes() {
            println!("\tcaused by: {}", cause);
        }
    }
}

async fn true_main() -> Result<(), failure::Error> {
    pretty_env_logger::init();

    let containerd_sock = PathBuf::from("/run/containerd/containerd.sock");
    let tcp_proxy_port = 9090;

    // spin up the sock_to_tcp_proxy proxy server
    let (tx, rx) = oneshot::channel::<()>();
    tokio::spawn(async move {
        if let Err(e) = sock_to_tcp_proxy::server(containerd_sock, tcp_proxy_port, tx).await {
            error!("sock_to_tcp_proxy server error: {}", e);
        }
    });
    rx.await?;

    let mut client = VersionClient::connect(format!("http://localhost:{}", tcp_proxy_port))?;

    let request = tonic::Request::new(());
    let response = client.version(request).await?;

    println!("{:?}", response);

    Ok(())
}
