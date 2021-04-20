// Copyright (c) Microsoft. All rights reserved.

use std::net::ToSocketAddrs;

use failure::{Fail, ResultExt};
use futures::future::join_all;
use futures::sync::oneshot;
use futures::sync::oneshot::Receiver;
use futures::{Future, IntoFuture};
use hyper::Server;
use log::{debug, info, warn};
use tokio::runtime::current_thread::Runtime;

use crate::api::ApiService;
use crate::proxy::{get_config, Client, ProxyService};
use crate::signal::ShutdownSignal;
use crate::{ApiSettings, Error, ErrorKind, InitializeErrorReason, ServiceSettings, Settings};

pub struct Routine {
    settings: Settings,
}

impl Routine {
    pub fn new(settings: Settings) -> Self {
        Routine { settings }
    }

    pub fn run_until(&self, signal: ShutdownSignal) -> Result<(), Error> {
        if self.settings.services().is_empty() {
            warn!("No proxy services specified in config file");
        } else {
            let mut servers: Vec<Box<dyn Future<Item = (), Error = Error> + Send>> = Vec::new();
            let mut senders = Vec::new();

            for settings in self.settings.services().iter() {
                let (tx, rx) = oneshot::channel();
                senders.push(tx);

                let proxy = start_proxy(&settings, rx);
                servers.push(Box::new(proxy));
            }

            let (tx, rx) = oneshot::channel();
            senders.push(tx);

            if let Some(settings) = self.settings.api() {
                let api = start_api(settings, rx);
                servers.push(Box::new(api));
            }

            let shutdown_signal = signal.map(move |_| {
                debug!("Shutdown signalled. Starting to shutdown services");
                for tx in senders {
                    tx.send(()).unwrap_or(())
                }
            });

            let mut runtime =
                Runtime::new().context(ErrorKind::Initialize(InitializeErrorReason::Tokio))?;

            runtime.spawn(shutdown_signal);
            runtime.block_on(join_all(servers))?;
        }

        info!("Shutdown completed");

        Ok(())
    }
}

fn start_api(
    settings: &ApiSettings,
    shutdown: Receiver<()>,
) -> impl Future<Item = (), Error = Error> {
    let settings = settings.clone();

    info!("Starting api server {}", settings.entrypoint());

    settings
        .entrypoint()
        .to_socket_addrs()
        .map_err(|err| {
            Error::from(
                err.context(ErrorKind::Initialize(InitializeErrorReason::InvalidUrl(
                    settings.entrypoint().clone(),
                ))),
            )
        })
        .and_then(|mut addrs| {
            addrs.next().ok_or_else(|| {
                Error::from(ErrorKind::Initialize(
                    InitializeErrorReason::InvalidUrlWithReason(
                        settings.entrypoint().clone(),
                        "URL has no address".to_string(),
                    ),
                ))
            })
        })
        .map(move |addr| {
            let new_service = ApiService::new();

            let server = Server::bind(&addr)
                .serve(new_service)
                .with_graceful_shutdown(shutdown)
                .map_err(|err| Error::from(err.context(ErrorKind::ApiService)));

            info!(
                "Listening on {} with 1 thread for api",
                settings.entrypoint(),
            );

            server
        })
        .into_future()
        .flatten()
}

fn start_proxy(
    settings: &ServiceSettings,
    shutdown: Receiver<()>,
) -> impl Future<Item = (), Error = Error> {
    let settings = settings.clone();

    info!(
        "Starting proxy server {} {}",
        settings.name(),
        settings.entrypoint()
    );

    settings
        .entrypoint()
        .to_socket_addrs()
        .map_err(|err| {
            Error::from(
                err.context(ErrorKind::Initialize(InitializeErrorReason::InvalidUrl(
                    settings.entrypoint().clone(),
                ))),
            )
        })
        .and_then(|mut addrs| {
            addrs.next().ok_or_else(|| {
                Error::from(ErrorKind::Initialize(
                    InitializeErrorReason::InvalidUrlWithReason(
                        settings.entrypoint().clone(),
                        "URL has no address".to_string(),
                    ),
                ))
            })
        })
        .and_then(move |addr| {
            let config = get_config(&settings)?;
            let client = Client::new(config);
            let new_service = ProxyService::new(client);

            let server = Server::bind(&addr)
                .serve(new_service)
                .with_graceful_shutdown(shutdown)
                .map_err(|err| Error::from(err.context(ErrorKind::ProxyService)));

            info!(
                "Listening on {} with 1 thread for {}",
                settings.entrypoint(),
                settings.name()
            );

            Ok(server)
        })
        .into_future()
        .flatten()
}

#[allow(unused_imports)]
#[cfg(test)]
mod tests {
    use std::fs;
    use std::net::TcpListener;
    use std::path::Path;
    use std::str::FromStr;

    use futures::future::Future;
    use futures::sync::oneshot;
    use hyper::{Body, Client, StatusCode, Uri};
    use hyper_tls::HttpsConnector;
    use tempfile::TempDir;
    use tokio::runtime::current_thread::Runtime;
    use url::Url;

    use crate::proxy::test::http::get_unused_tcp_port;
    use crate::routine::{start_api, start_proxy};
    use crate::{logging, ApiSettings, ErrorKind, InitializeErrorReason, ServiceSettings};

    #[test]
    fn it_runs_proxy() {
        let dir = TempDir::new().unwrap();

        let token = dir.path().join("token");
        fs::write(&token, "token").unwrap();

        let settings = ServiceSettings::new(
            "management".to_owned(),
            Url::parse(format!("http://localhost:{}", get_unused_tcp_port()).as_str()).unwrap(),
            Url::parse("https://iotedge:30000").unwrap(),
            None,
            &token,
        );

        let (tx, rx) = oneshot::channel();

        let proxy = start_proxy(&settings, rx);

        let mut runtime = Runtime::new().unwrap();
        runtime.spawn(proxy.map_err(|err| println!("{:?}", err)));

        let client = Client::new();
        let task = client.get(Uri::from_str(settings.entrypoint().as_str()).unwrap());

        let res = runtime.block_on(task).unwrap();
        assert_eq!(res.status(), StatusCode::BAD_GATEWAY);

        tx.send(()).unwrap();
    }

    #[test]
    fn it_fails_start_proxy_when_entrypoint_is_invalid() {
        let settings = ServiceSettings::new(
            "management".to_owned(),
            Url::parse("http://127.0.0.1.com").unwrap(),
            Url::parse("https://iotedged:30000").unwrap(),
            None,
            Path::new("token"),
        );

        let (_, rx) = oneshot::channel();

        let proxy = start_proxy(&settings, rx);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(proxy).unwrap_err();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::InvalidUrl(
                settings.entrypoint().clone()
            ))
        );
    }

    #[test]
    fn it_fails_start_proxy_when_proxy_client_config_invalid() {
        let settings = ServiceSettings::new(
            "management".to_owned(),
            Url::parse("http://localhost:3000").unwrap(),
            Url::parse("https://iotedged:30000").unwrap(),
            None,
            Path::new("token"),
        );

        let (_, rx) = oneshot::channel();

        let proxy = start_proxy(&settings, rx);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(proxy).unwrap_err();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::ClientConfigReadFile(
                "token".to_string()
            ))
        );
    }

    #[test]
    fn it_runs_api() {
        let settings = ApiSettings::new(
            Url::parse(format!("http://localhost:{}", get_unused_tcp_port()).as_str()).unwrap(),
        );

        let (tx, rx) = oneshot::channel();

        let proxy = start_api(&settings, rx);

        let mut runtime = Runtime::new().unwrap();
        runtime.spawn(proxy.map_err(|err| println!("{:?}", err)));

        let client = Client::new();
        let task = client
            .get(Uri::from_str(settings.entrypoint().join("/health").unwrap().as_str()).unwrap());

        let res = runtime.block_on(task).unwrap();
        assert_eq!(res.status(), StatusCode::OK);

        tx.send(()).unwrap();
    }

    #[test]
    fn it_fails_start_api_when_entrypoint_is_invalid() {
        let settings = ApiSettings::new(Url::parse("http://127.0.0.1.com").unwrap());

        let (_, rx) = oneshot::channel();

        let proxy = start_api(&settings, rx);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(proxy).unwrap_err();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::InvalidUrl(
                settings.entrypoint().clone()
            ))
        );
    }
}
