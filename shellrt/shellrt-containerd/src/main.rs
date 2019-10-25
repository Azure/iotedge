use std::path::PathBuf;

use failure::ResultExt;
use log::*;
use tokio::sync::oneshot;

use containerd_grpc::containerd::services::version::v1::client::VersionClient;

use shellrt_api::v0::{
    plugin::{Input, Output, Request, Response},
    request, response, VERSION,
};

mod error;
mod sock_to_tcp_proxy;

use error::{ErrorKind, Result};

#[tokio::main]
async fn main() {
    // first, spin up the sock_to_tcp_proxy server
    let containerd_sock = PathBuf::from("/run/containerd/containerd.sock");
    let tcp_proxy_port = 9090;

    let (tx, rx) = oneshot::channel::<()>();
    tokio::spawn(async move {
        if let Err(e) = sock_to_tcp_proxy::server(containerd_sock, tcp_proxy_port, tx).await {
            error!("sock_to_tcp_proxy server error: {}", e);
        }
    });
    rx.await.unwrap();

    let grpc_uri = format!("http://localhost:{}", tcp_proxy_port);

    let response = handle_input(grpc_uri).await.map_err(|e| e.into());
    let output = Output::new(response);

    info!("{:#?}", output);

    serde_json::to_writer_pretty(std::io::stdout(), &output)
        .expect("failed to write output to stdout");
}

async fn handle_input(grpc_uri: String) -> Result<Response> {
    pretty_env_logger::init();

    // parse a pull request from stdin
    let input: Input =
        serde_json::from_reader(std::io::stdin()).context(ErrorKind::InvalidRequest)?;

    if input.version() != VERSION {
        return Err(ErrorKind::IncompatibleVersion.into());
    }

    info!("{:#?}", input);

    match input.into_inner() {
        Request::Pull(request::Pull { image, credentials }) => {
            // TODO: the actual code to handle this lol

            Ok(Response::Pull(response::Pull {}))
        }
        Request::RuntimeVersion(request::RuntimeVersion {}) => {
            let mut client = VersionClient::connect(grpc_uri).unwrap();

            let request = tonic::Request::new(());
            let response = client.version(request).await.unwrap().into_inner();

            Ok(Response::RuntimeVersion(response::RuntimeVersion {
                info: format!(
                    "shellrt-containerd {}\ncontainerd {} rev {}",
                    env!("CARGO_PKG_VERSION"),
                    response.version,
                    response.revision,
                ),
            }))
        }
        _ => Err(ErrorKind::UnimplementedReq.into()),
    }
}
