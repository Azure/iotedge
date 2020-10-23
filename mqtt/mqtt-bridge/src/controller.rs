use std::{collections::HashMap, sync::Arc};

use futures_util::future::join_all;
use thiserror::Error;
use tokio::{
    stream::StreamExt,
    sync::{
        mpsc::{self, UnboundedReceiver, UnboundedSender},
        Mutex,
    },
};
use tracing::{debug, error, info, info_span};
use tracing_futures::Instrument;

use crate::{
    bridge::{Bridge, BridgeError},
    config_update::{BridgeControllerUpdate, ConfigUpdater},
    settings::BridgeSettings,
};

const UPSTREAM: &str = "$upstream";

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
pub struct BridgeController {
    handle: BridgeControllerHandle,
    updates_receiver: UnboundedReceiver<BridgeControllerUpdate>,
    bridge_handles: Arc<Mutex<HashMap<String, ConfigUpdater>>>,
}

impl BridgeController {
    pub fn new() -> Self {
        let (sender, updates_receiver) = mpsc::unbounded_channel();
        let handle = BridgeControllerHandle { sender };

        Self {
            handle,
            updates_receiver,
            bridge_handles: Arc::new(Mutex::new(HashMap::new())),
        }
    }

    pub fn handle(&self) -> BridgeControllerHandle {
        self.handle.clone()
    }

    pub async fn run(
        mut self,
        system_address: String,
        device_id: String,
        settings: BridgeSettings,
    ) {
        info!("starting bridge controller...");

        let mut bridge_tasks = vec![];
        if let Some(upstream_settings) = settings.upstream() {
            let name = upstream_settings.name().to_owned();

            let bridge =
                Bridge::new_upstream(system_address, device_id, upstream_settings.clone()).await;

            match bridge {
                Ok(bridge) => {
                    let bridge_handle = bridge.handle();
                    let bridge_name = name.clone();

                    self.bridge_handles
                        .lock()
                        .await
                        .insert(bridge_name.clone(), ConfigUpdater::new(bridge_handle));

                    let upstream_bridge = async move {
                        if let Err(e) = bridge.run().await {
                            error!(err = %e, "failed running {} bridge", name);
                        }
                    }
                    .instrument(info_span!("bridge", name = UPSTREAM));

                    // bridge running before sending initial settings
                    let task = tokio::spawn(upstream_bridge);

                    // send initial subscription configuration
                    if let Err(e) =
                        self.handle
                            .send(BridgeControllerUpdate::from_bridge_topic_rules(
                                &bridge_name,
                                &upstream_settings.subscriptions(),
                                &upstream_settings.forwards(),
                            ))
                    {
                        error!(
                            "failed to send initial subscriptions for {}. {}",
                            bridge_name, e
                        );
                    }

                    bridge_tasks.push(task);
                }
                Err(e) => {
                    error!(err = %e, "failed to create {} bridge", upstream_settings.name());
                }
            };
        } else {
            info!("No upstream settings detected. Not starting bridge controller.")
        };

        let updates = async move {
            while let Some(update) = self.updates_receiver.next().await {
                // for now only supports upstream bridge.
                for bridge_update in update.bridge_updates() {
                    debug!("received updated config: {:?}", bridge_update);

                    if bridge_update.endpoint() != UPSTREAM {
                        continue;
                    }

                    if let Some(config) = self.bridge_handles.lock().await.get_mut(UPSTREAM) {
                        if let Err(e) = config.send_update(bridge_update.to_owned()).await {
                            error!("error sending bridge update {:?}", e);
                        }
                    }
                }
            }
        }
        .instrument(info_span!("controller", name = "updates"));

        // join_all is fine because the bridge shouldn't fail and exit
        // if a pump in the bridge fails, it should internally recreate it
        // this means that if a bridge stops, then shutdown was triggered
        futures_util::future::join(join_all(bridge_tasks), updates).await;
    }
}

impl Default for BridgeController {
    fn default() -> Self {
        Self::new()
    }
}

#[derive(Clone, Debug)]
pub struct BridgeControllerHandle {
    sender: UnboundedSender<BridgeControllerUpdate>,
}

impl BridgeControllerHandle {
    pub fn send(&mut self, message: BridgeControllerUpdate) -> Result<(), Error> {
        self.sender
            .send(message)
            .map_err(Error::SendControllerMessage)
    }
}

#[derive(Debug, Error)]
pub enum Error {
    #[error("An error occurred sending a message to the controller.")]
    SendControllerMessage(#[source] tokio::sync::mpsc::error::SendError<BridgeControllerUpdate>),

    #[error("An error occurred sending a message to the bridge.")]
    SendBridgeMessage(#[from] BridgeError),
}
