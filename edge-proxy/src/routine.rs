// Copyright (c) Microsoft. All rights reserved.

use std::net::ToSocketAddrs;

use failure::{Fail, ResultExt};
use futures::future::join_all;
use futures::{Future, IntoFuture};
use hyper::Server;
use log::{debug, info, warn};
use tokio::runtime::Runtime;
use tokio::sync::oneshot;
use tokio::sync::oneshot::Receiver;

use crate::api::ApiService;
use crate::proxy::{get_config, Client, ProxyService};
use crate::signal::ShutdownSignal;
use crate::{ApiSettings, Error, ErrorKind, ServiceSettings, Settings};

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

            let mut runtime = Runtime::new().context(ErrorKind::Tokio)?;
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
            Error::from(err.context(ErrorKind::InvalidUrl(settings.entrypoint().to_string())))
        })
        .and_then(|mut addrs| {
            addrs.next().ok_or_else(|| {
                let err = ErrorKind::InvalidUrlWithReason(
                    settings.entrypoint().to_string(),
                    "URL has no address".to_string(),
                );
                Error::from(err)
            })
        })
        .and_then(move |addr| {
            let new_service = ApiService::new();

            let server = Server::bind(&addr)
                .serve(new_service)
                .with_graceful_shutdown(shutdown)
                .map_err(Error::from);

            info!(
                "Listening on {} with 1 thread for api",
                settings.entrypoint(),
            );

            Ok(server)
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
            Error::from(err.context(ErrorKind::InvalidUrl(settings.entrypoint().to_string())))
        })
        .and_then(|mut addrs| {
            addrs.next().ok_or_else(|| {
                let err = ErrorKind::InvalidUrlWithReason(
                    settings.entrypoint().to_string(),
                    "URL has no address".to_string(),
                );
                Error::from(err)
            })
        })
        .and_then(move |addr| {
            let config = get_config(&settings)?;
            let client = Client::new(config);
            let new_service = ProxyService::new(client);

            let server = Server::bind(&addr)
                .serve(new_service)
                .with_graceful_shutdown(shutdown)
                .map_err(Error::from);

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
