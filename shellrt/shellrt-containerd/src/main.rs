use std::path::PathBuf;
use std::pin::Pin;

use failure::ResultExt;
use log::*;
use tokio::prelude::*;
use tokio::sync::oneshot;

use shellrt_api::v0::{
    plugin::{Input, Output, Request, Response},
    VERSION,
};

mod error;
mod sock_to_tcp_proxy;
mod util;

use error::*;

pub type ResponseThunk =
    Box<dyn FnOnce(Pin<Box<dyn AsyncWrite>>) -> Pin<Box<dyn Future<Output = std::io::Result<()>>>>>;

macro_rules! handlers {
    (
        + $(-)*      + $(-)*      + $(-)*          +
        | message    | module     | handler        |
        + $(-)*      + $(-)*      + $(-)*          +
      $(| $msg:ident | $mod:ident | $handler:ident |)*
        + $(-)*      + $(-)*      + $(-)*          +
    ) => {
        mod handler {
            $(mod $mod;)*
            $(pub use $mod::$handler as $msg;)*
        }

        async fn handle_message(grpc_uri: String, req: Request) -> Result<(Response, Option<ResponseThunk>)> {
            match req {
                $(Request::$msg(req) => {
                    let (response, thunk) = handler::$msg::new(grpc_uri).handle(req).await?;
                    Ok((Response::$msg(response), thunk))
                })*
            }
        }
    };
}

handlers! {
    +-----------+------------+------------------+
    | message   | module     | handler          |
    +-----------+------------+------------------+
    | Create    | create     | CreateHandler    |
    | ImgPull   | img_pull   | ImgPullHandler   |
    | ImgRemove | img_remove | ImgRemoveHandler |
    | List      | list       | ListHandler      |
    | Logs      | logs       | LogsHandler      |
    | Remove    | remove     | RemoveHandler    |
    | Restart   | restart    | RestartHandler   |
    | Start     | start      | StartHandler     |
    | Status    | status     | StatusHandler    |
    | Stop      | stop       | StopHandler      |
    | Version   | version    | VersionHandler   |
    +-----------+------------+------------------+
}

#[tokio::main]
async fn main() -> std::result::Result<(), Box<dyn std::error::Error>> {
    pretty_env_logger::init();

    // TODO: how to specify containerd socket path?
    //       sent on each request? loaded from config file?
    let containerd_sock = PathBuf::from("/run/containerd/containerd.sock");
    let tcp_proxy_port = 9090;

    if !containerd_sock.exists() {
        let err = Error::new(ErrorKind::GrpcConnect.into());
        let output = Output::new(Err(err.into()));
        serde_json::to_writer_pretty(std::io::stdout(), &output)?;
        return Ok(());
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
    let (res, thunk) = match handle_input(grpc_uri).await {
        Ok((res, thunk)) => (Ok(res), thunk),
        Err(e) => (Err(e), None),
    };

    let output = Output::new(res.map_err(Into::into));
    info!("{:#?}", output);

    // dump response to stdout
    serde_json::to_writer(std::io::stdout(), &output)?;

    // if a streaming response thunk is provided, run it now.
    if let Some(thunk) = thunk {
        let mut stdout = tokio::io::stdout();
        stdout.write_all(b"\x00").await?;
        thunk(Box::pin(stdout)).await?;
    }

    Ok(())
}

async fn handle_input(grpc_uri: String) -> Result<(Response, Option<ResponseThunk>)> {
    // parse an incoming request
    let input: Input =
        serde_json::from_reader(std::io::stdin()).context(ErrorKind::InvalidRequest)?;
    info!("{:#?}", input);

    // TODO: use semver for more lenient version compatibility
    if input.version() != VERSION {
        return Err(ErrorKind::IncompatibleVersion.into());
    }

    let res = handle_message(grpc_uri, input.into_inner()).await?;

    Ok(res)
}
