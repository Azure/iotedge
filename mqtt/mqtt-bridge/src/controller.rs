#![allow(unused_imports, unused_variables, dead_code)]
use std::collections::HashMap;

use futures_util::{
    future::{self, BoxFuture, Either, Map},
    pin_mut,
    stream::{FuturesUnordered, Stream},
    FusedStream, FutureExt, StreamExt,
};
use thiserror::Error;
use tokio::{
    sync::mpsc::{self, UnboundedReceiver, UnboundedSender},
    task::{JoinError, JoinHandle},
};
use tracing::{debug, error, info, info_span, warn};
use tracing_futures::Instrument;

use crate::{
    bridge::{Bridge, BridgeError, BridgeHandle},
    config_update::BridgeUpdate,
    config_update::{BridgeControllerUpdate, ConfigUpdater},
    persist::StreamWakeableState,
    settings::BridgeSettings,
    settings::ConnectionSettings,
};

const UPSTREAM: &str = "$upstream";

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
pub struct BridgeController {
    handle: BridgeControllerHandle,
    messages: UnboundedReceiver<BridgeControllerMessage>,
}

impl BridgeController {
    pub fn new() -> Self {
        let (sender, updates_receiver) = mpsc::unbounded_channel();
        let handle = BridgeControllerHandle { sender };

        Self {
            handle,
            messages: updates_receiver,
        }
    }

    pub fn handle(&self) -> BridgeControllerHandle {
        self.handle.clone()
    }

    pub async fn run(self, system_address: String, device_id: String, settings: BridgeSettings) {
        info!("starting bridge controller...");

        let mut bridges = Bridges::default();

        if let Some(upstream_settings) = settings.upstream() {
            match Bridge::new_upstream(&system_address, &device_id, upstream_settings) {
                Ok(bridge) => {
                    bridges.start_bridge(bridge, upstream_settings).await;
                }
                Err(e) => {
                    error!(err = %e, "failed to create {} bridge", UPSTREAM);
                }
            }
        } else {
            info!("No upstream settings detected.")
        }

        let messages = self.messages.fuse();
        pin_mut!(messages);

        let mut no_bridges = bridges.is_terminated();

        loop {
            let any_bridge = if no_bridges {
                // if no active bridges available, wait only for a new messages arrival
                Either::Left(future::pending())
            } else {
                // otherwise try to await both a new message arrival or any bridge exit
                Either::Right(bridges.next())
            };

            match future::select(messages.select_next_some(), any_bridge).await {
                Either::Left((BridgeControllerMessage::BridgeControllerUpdate(update), _)) => {
                    process_update(update, &mut bridges).await
                }
                Either::Left((BridgeControllerMessage::Shutdown, _)) => {
                    info!("bridge controller shutdown requested");
                    bridges.shutdown_all().await;
                    break;
                }
                Either::Right((Some((name, bridge)), _)) => {
                    match bridge {
                        Ok(Ok(_)) => debug!("bridge {} exited", name),
                        Ok(Err(e)) => warn!(error = %e, "bridge {} exited with error", name),
                        Err(e) => warn!(error = %e, "bridge {} paniced ", name),
                    }

                    info!("restarting bridge {}...", name);
                    if let Some(upstream_settings) = settings.upstream() {
                        match Bridge::new_upstream(&system_address, &device_id, upstream_settings) {
                            Ok(bridge) => {
                                bridges.start_bridge(bridge, upstream_settings).await;
                            }
                            Err(e) => {
                                error!(err = %e, "failed to create {} bridge", name);
                            }
                        }
                    }
                }
                Either::Right((None, _)) => {
                    // first time we resolve any_bridges future it returns None
                    no_bridges = true;
                }
            }
        }

        info!("finished bridge controller");
    }
}

async fn process_update(update: BridgeControllerUpdate, bridges: &mut Bridges) {
    debug!("received updated config: {:?}", update);

    for bridge_update in update.into_inner() {
        // for now only supports upstream bridge.
        if bridge_update.name() != UPSTREAM {
            warn!(
                "updates for {} bridge is not supported",
                bridge_update.name()
            );
            continue;
        }

        bridges.send_update(bridge_update).await;
    }
}

type BridgeFuture = BoxFuture<'static, (String, Result<Result<(), BridgeError>, JoinError>)>;

#[derive(Default)]
pub struct Bridges {
    bridge_handles: HashMap<String, BridgeHandle>,
    config_updaters: HashMap<String, ConfigUpdater>,
    bridges: FuturesUnordered<BridgeFuture>,
}

impl Bridges {
    async fn start_bridge<S>(&mut self, bridge: Bridge<S>, settings: &ConnectionSettings)
    where
        S: StreamWakeableState + Send + 'static,
    {
        let name = settings.name().to_owned();

        let bridge_handle = bridge.handle();
        let mut config_updater = ConfigUpdater::new(bridge.handle());

        let bridge_name = settings.name().to_owned();
        let upstream_bridge = bridge
            .run()
            .instrument(info_span!("bridge", name = %bridge_name));

        // bridge running before sending initial settings
        let task = tokio::spawn(upstream_bridge).map({
            let name = name.clone();
            move |res| (name, res)
        });

        // send initial subscription configuration
        let update = BridgeUpdate::new(name.clone(), settings.subscriptions(), settings.forwards());
        if let Err(e) = config_updater.send_update(update).await {
            error!("failed to send initial subscriptions for {}. {}", name, e);
        }

        self.bridge_handles.insert(name.clone(), bridge_handle);
        self.config_updaters.insert(name.clone(), config_updater);
        self.bridges.push(Box::pin(task));
    }

    async fn send_update(&mut self, update: BridgeUpdate) {
        if let Some(config) = self.config_updaters.get_mut(update.name()) {
            if let Err(e) = config.send_update(update).await {
                error!("error sending bridge update {:?}", e);
            }
        }
    }

    async fn shutdown_all(&mut self) {
        debug!("sending shutdown request to all bridges...");

        // sending shutdown signal to each bridge
        let shutdowns = self
            .bridge_handles
            .drain()
            .map(|(_, handle)| handle.shutdown());
        future::join_all(shutdowns).await;

        // wait until all bridges finish
        while let Some((name, bridge)) = self.bridges.next().await {
            match bridge {
                Ok(Ok(_)) => debug!("bridge {} exited", name),
                Ok(Err(e)) => warn!(error = %e, "bridge {} exited with error", name),
                Err(e) => warn!(error = %e, "bridge {} panicked ", name),
            }
        }
        debug!("all bridges exited");
    }
}

impl Stream for Bridges {
    type Item = (
        String,
        Result<Result<(), BridgeError>, tokio::task::JoinError>,
    );

    fn poll_next(
        mut self: std::pin::Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> std::task::Poll<Option<Self::Item>> {
        let poll = self.bridges.poll_next_unpin(cx);

        // remove redundant handlers when bridge exits
        if let std::task::Poll::Ready(Some((name, _))) = &poll {
            self.bridge_handles.remove(name);
            self.config_updaters.remove(name);
        }

        poll
    }
}

impl FusedStream for Bridges {
    fn is_terminated(&self) -> bool {
        self.bridges.is_terminated()
    }
}

impl Default for BridgeController {
    fn default() -> Self {
        Self::new()
    }
}

#[derive(Clone, Debug)]
pub struct BridgeControllerHandle {
    sender: UnboundedSender<BridgeControllerMessage>,
}

#[derive(Debug)]
pub enum BridgeControllerMessage {
    BridgeControllerUpdate(BridgeControllerUpdate),
    Shutdown,
}

impl BridgeControllerHandle {
    pub fn send_update(&mut self, update: BridgeControllerUpdate) -> Result<(), Error> {
        self.send_message(BridgeControllerMessage::BridgeControllerUpdate(update))
    }

    pub fn shutdown(mut self) {
        if let Err(e) = self.send_message(BridgeControllerMessage::Shutdown) {
            error!(error = %e, "unable to request shutdown for bridge controller");
        }
    }

    fn send_message(&mut self, message: BridgeControllerMessage) -> Result<(), Error> {
        self.sender
            .send(message)
            .map_err(Error::SendControllerMessage)
    }
}

#[derive(Debug, Error)]
pub enum Error {
    #[error("An error occurred sending a message to the controller.")]
    SendControllerMessage(#[source] tokio::sync::mpsc::error::SendError<BridgeControllerMessage>),

    #[error("An error occurred sending a message to the bridge.")]
    SendBridgeMessage(#[from] BridgeError),
}
