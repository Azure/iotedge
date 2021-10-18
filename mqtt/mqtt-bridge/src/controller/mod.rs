mod bridges;

use std::collections::HashMap;

use bridges::Bridges;

use async_trait::async_trait;
use futures_util::{
    future::{self, Either},
    stream::{Fuse, FusedStream},
    StreamExt,
};
use tokio::sync::mpsc::{self, UnboundedSender};
use tokio_stream::wrappers::UnboundedReceiverStream;
use tracing::{debug, error, info};

use mqtt_broker::sidecar::{Sidecar, SidecarShutdownHandle, SidecarShutdownHandleError};

use crate::{
    bridge::{Bridge, BridgeError},
    config_update::{BridgeControllerUpdate, BridgeUpdate},
    persist::{RingBuffer, WakingMemoryStore},
    settings::{BridgeSettings, StorageSettings},
};

const UPSTREAM: &str = "$upstream";

/// `BridgeController` controls lifetime of bridges: start/stop and update
/// forwarding rules.
///
/// Controller handles monitors settings updates and starts a new `Bridge` or
/// stops running `Bridge` if the number of bridges changes. In addition it
/// prepares changes in forwarding rules and applies them to `Bridge` if required.
pub struct BridgeController {
    system_address: String,
    device_id: String,
    settings: BridgeSettings,
    handle: BridgeControllerHandle,
    messages: Fuse<UnboundedReceiverStream<BridgeControllerMessage>>,
}

impl BridgeController {
    pub fn new(system_address: String, device_id: String, settings: BridgeSettings) -> Self {
        let (sender, updates_receiver) = mpsc::unbounded_channel();
        let handle = BridgeControllerHandle { sender };

        Self {
            system_address,
            device_id,
            settings,
            handle,
            messages: UnboundedReceiverStream::new(updates_receiver).fuse(),
        }
    }

    pub fn handle(&self) -> BridgeControllerHandle {
        self.handle.clone()
    }

    async fn start_bridge(&self, bridges: &mut Bridges) {
        if let Some(upstream_settings) = self.settings.upstream() {
            let storage_settings = self.settings.storage();
            match storage_settings {
                StorageSettings::Memory(memory_settings) => {
                    match Bridge::<WakingMemoryStore>::new_upstream(
                        &self.system_address,
                        &self.device_id,
                        upstream_settings,
                        memory_settings.clone(),
                    ) {
                        Ok(bridge) => {
                            bridges.start_bridge(bridge, upstream_settings).await;
                        }
                        Err(e) => {
                            error!(err = %e, "failed to create {} bridge", UPSTREAM);
                        }
                    }
                }
                StorageSettings::RingBuffer(ring_buffer_settings) => {
                    match Bridge::<RingBuffer>::new_upstream(
                        &self.system_address,
                        &self.device_id,
                        upstream_settings,
                        ring_buffer_settings.clone(),
                    ) {
                        Ok(bridge) => {
                            bridges.start_bridge(bridge, upstream_settings).await;
                        }
                        Err(e) => {
                            error!(err = %e, "failed to create {} bridge", UPSTREAM);
                        }
                    }
                }
            }
        } else {
            info!("no upstream settings detected");
        }
    }
}

#[async_trait]
impl Sidecar for BridgeController {
    fn shutdown_handle(&self) -> Result<SidecarShutdownHandle, SidecarShutdownHandleError> {
        let handle = self.handle.clone();
        Ok(SidecarShutdownHandle::new(async { handle.shutdown() }))
    }

    async fn run(mut self: Box<Self>) {
        info!("starting bridge controller...");

        let mut bridges = Bridges::default();

        self.start_bridge(&mut bridges).await;

        loop {
            let wait_bridge_or_pending = if bridges.is_terminated() {
                // if bridges were terminated just wait for messages
                Either::Left(future::pending())
            } else {
                // otherwise try to await both a new message arrival or any bridge exit
                Either::Right(bridges.next())
            };

            match future::select(self.messages.select_next_some(), wait_bridge_or_pending).await {
                Either::Left((BridgeControllerMessage::BridgeControllerUpdate(update), _)) => {
                    process_update(update, &mut bridges).await;
                }
                Either::Left((BridgeControllerMessage::Shutdown, _)) => {
                    info!("bridge controller shutdown requested");
                    bridges.shutdown_all().await;
                    break;
                }
                Either::Left((BridgeControllerMessage::ShutdownBridge(name), _)) => {
                    info!("bridge {} shutdown requested", name);
                    bridges.shutdown_bridge(&name).await;
                }
                Either::Right((Some((name, bridge)), _)) => {
                    match bridge {
                        Ok(Ok(_)) => debug!("bridge {} exited", name),
                        Ok(Err(e)) => error!(error = %e, "bridge {} exited with error", name),
                        Err(e) => error!(error = %e, "bridge {} panicked ", name),
                    };

                    // always restart upstream bridge
                    if name == UPSTREAM {
                        info!("restarting bridge...");
                        self.start_bridge(&mut bridges).await;
                    }
                }
                Either::Right((None, _)) => {
                    // first time we resolve bridge future it returns None
                }
            }
        }

        info!("bridge controller stopped");
    }
}

async fn process_update(update: BridgeControllerUpdate, bridges: &mut Bridges) {
    debug!("received updated config: {:?}", update);

    let mut bridge_updates = update
        .into_inner()
        .into_iter()
        .map(|update| (update.endpoint().to_owned(), update))
        .collect::<HashMap<_, _>>();

    // for now only supports upstream bridge.
    if let Some(bridge_update) = bridge_updates.remove(UPSTREAM) {
        bridges.send_update(bridge_update).await;
    } else {
        debug!("{} bridge update is empty", UPSTREAM);
        bridges.send_update(BridgeUpdate::new(UPSTREAM)).await;
    }
}

#[derive(Clone, Debug)]
pub struct BridgeControllerHandle {
    sender: UnboundedSender<BridgeControllerMessage>,
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

    pub fn shutdown_bridge(&mut self, name: &str) {
        if let Err(e) = self.send_message(BridgeControllerMessage::ShutdownBridge(name.into())) {
            error!(error = %e, "unable to request shutdown for bridge {}", name);
        }
    }

    fn send_message(&mut self, message: BridgeControllerMessage) -> Result<(), Error> {
        self.sender
            .send(message)
            .map_err(Error::SendControllerMessage)
    }
}

/// Control message for `BridgeController`.
#[derive(Debug)]
pub enum BridgeControllerMessage {
    BridgeControllerUpdate(BridgeControllerUpdate),
    // Shutdown all bridges
    Shutdown,
    // Shutdown a bridge by name. $upstream bridge will be recreated if it is shutdown
    ShutdownBridge(String),
}

/// Error for `BridgeController`.
#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("An error occurred sending a message to the controller. Caused by: {0}")]
    SendControllerMessage(#[source] tokio::sync::mpsc::error::SendError<BridgeControllerMessage>),

    #[error("An error occurred sending a message to the bridge. Caused by: {0}")]
    SendBridgeMessage(#[from] BridgeError),
}
