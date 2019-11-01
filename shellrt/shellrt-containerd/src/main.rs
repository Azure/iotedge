use std::path::PathBuf;

use failure::ResultExt;
use log::*;
use tokio::sync::oneshot;

use shellrt_api::v0::{
    plugin::{Input, Output, Request, Response},
    VERSION,
};

mod error;
mod handler;
mod sock_to_tcp_proxy;
mod util;

use error::*;

#[tokio::main]
async fn main() {
    pretty_env_logger::init();

    // TODO: how to specify containerd socket path?
    //       sent on each request? loaded from config file?
    let containerd_sock = PathBuf::from("/run/containerd/containerd.sock");
    let tcp_proxy_port = 9090;

    if !containerd_sock.exists() {
        let err = Error::new(ErrorKind::GrpcConnect.into());
        let output = Output::new(Err(err.into()));
        serde_json::to_writer_pretty(std::io::stdout(), &output)
            .expect("failed to write output to stdout");
        return;
    }

    // HACK: remove sock_to_tcp_proxy::server once tonic supports custom transports
    // Use a oneshot to ensure the proxy server is ready to receive gRPC clients.
    let (tx, rx) = oneshot::channel::<()>();
    tokio::spawn(async move {
        if let Err(e) = sock_to_tcp_proxy::server(containerd_sock, tcp_proxy_port, tx).await {
            error!("sock_to_tcp_proxy server error: {}", e);
        }
    });
    rx.await.unwrap();
    let grpc_uri = format!("http://localhost:{}", tcp_proxy_port);

    // handle incoming input
    let output = Output::new(handle_input(grpc_uri).await.map_err(Into::into));

    info!("{:#?}", output);

    serde_json::to_writer_pretty(std::io::stdout(), &output)
        .expect("failed to write output to stdout");
}

async fn handle_input(grpc_uri: String) -> Result<Response> {
    // parse an incoming request
    let input: Input =
        serde_json::from_reader(std::io::stdin()).context(ErrorKind::InvalidRequest)?;

    info!("{:#?}", input);

    // TODO: use semver for more lenient version compatibility
    if input.version() != VERSION {
        return Err(ErrorKind::IncompatibleVersion.into());
    }

    // TODO?: use a macro for these match statement
    let res = match input.into_inner() {
        Request::Pull(req) => Response::Pull(handler::Pull::new(grpc_uri).handle(req).await?),
        Request::Remove(req) => Response::Remove(handler::Remove::new(grpc_uri).handle(req).await?),
        Request::RuntimeVersion(req) => {
            Response::RuntimeVersion(handler::RuntimeVersion::new(grpc_uri).handle(req).await?)
        }
        _ => return Err(ErrorKind::UnimplementedReq.into()),
    };

    Ok(res)
}
