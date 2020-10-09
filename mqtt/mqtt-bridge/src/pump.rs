#![allow(dead_code)]
use std::{cell::RefCell, rc::Rc};

use futures_util::{
    future::{select, Either, FutureExt},
    pin_mut, select,
    stream::{StreamExt, TryStreamExt},
};
use tokio::sync::{mpsc::UnboundedReceiver, oneshot, oneshot::Receiver};
use tracing::{debug, error};

use mqtt3::{proto::Publication, proto::QoS::AtLeastOnce, PublishHandle};

use crate::{
    bridge::BridgeError,
    bridge::BridgeMessage,
    client::{ClientShutdownHandle, MqttClient},
    message_handler::MessageHandler,
    persist::{MessageLoader, PublicationStore, WakingMemoryStore},
};

const CONNECTIVITY_TOPIC: &str = "$internal/connectivity";

#[derive(Debug, Clone)]
pub enum PumpType {
    Local,
    Remote,
}

// TODO PRE: add enum for local or remote pump
pub struct Pump {
    client: MqttClient<MessageHandler<WakingMemoryStore>>,
    client_shutdown: ClientShutdownHandle,
    publish_handle: PublishHandle,
    subscriptions: Vec<String>,
    loader: Rc<RefCell<MessageLoader<WakingMemoryStore>>>,
    persist: PublicationStore<WakingMemoryStore>,
    connectivity_receiver: Option<UnboundedReceiver<BridgeMessage>>,
    pump_type: PumpType,
    bridge_name: String,
}

impl Pump {
    pub fn new(
        client: MqttClient<MessageHandler<WakingMemoryStore>>,
        subscriptions: Vec<String>,
        loader: Rc<RefCell<MessageLoader<WakingMemoryStore>>>,
        persist: PublicationStore<WakingMemoryStore>,
        connectivity_receiver: Option<UnboundedReceiver<BridgeMessage>>,
        pump_type: PumpType,
        bridge_name: String,
    ) -> Result<Self, BridgeError> {
        let publish_handle = client
            .publish_handle()
            .map_err(BridgeError::PublishHandle)?;
        let client_shutdown = client.shutdown_handle()?;

        Ok(Self {
            client,
            client_shutdown,
            publish_handle,
            subscriptions,
            loader,
            persist,
            connectivity_receiver,
            pump_type,
            bridge_name,
        })
    }

    pub async fn subscribe(&mut self) -> Result<(), BridgeError> {
        self.client
            .subscribe(&self.subscriptions)
            .await
            .map_err(BridgeError::Subscribe)?;

        Ok(())
    }

    // TODO PRE: Give logging context. Say which bridge pump.
    // TODO PRE: add comments
    // TODO PRE: clean up logging
    pub async fn run(&mut self, shutdown: Receiver<()>) {
        debug!("starting pumps for {} bridge...", self.bridge_name);

        let (loader_shutdown, loader_shutdown_rx) = oneshot::channel::<()>();
        let publish_handle = self.publish_handle.clone();
        let mut connectivity_publish_handle = self.publish_handle.clone();
        let persist = self.persist.clone();
        let loader = self.loader.clone();
        let mut client_shutdown = self.client_shutdown.clone();
        let receiver = self.connectivity_receiver.as_mut();
        let bridge_name = self.bridge_name.clone();
        let pump_type = self.pump_type.clone();

        // egress pump
        let f1 = async move {
            // TODO PRE: move this to init somehow
            let mut loader_borrow = loader.borrow_mut();
            let mut receive_fut = loader_shutdown_rx.into_stream();

            debug!(
                "{} bridge starting egress publication processing for {:?} pump...",
                bridge_name, pump_type
            );

            loop {
                let mut publish_handle = publish_handle.clone();
                match select(receive_fut.next(), loader_borrow.try_next()).await {
                    Either::Left((shutdown, _)) => {
                        debug!("egress pump received shutdown signal");
                        if let None = shutdown {
                            error!(message = "unexpected behavior from shutdown signal while signalling bridge pump shutdown")
                        }

                        debug!("bridge pump stopped");
                        break;
                    }
                    Either::Right((loaded_element, _)) => {
                        debug!("egress pump extracted publication from store");

                        if let Ok(Some((key, publication))) = loaded_element {
                            // TODO REVIEW: should we be retrying?
                            // if this failure is due to something that will keep failing it is probably safer to remove and never try again
                            // otherwise we should retry
                            debug!("publishing publication {:?} for egress pump", key);
                            if let Err(e) = publish_handle.publish(publication).await {
                                error!(message = "failed publishing publication for bridge pump", err = %e);
                            }

                            // TODO REVIEW: if removal fails, what do we do?
                            if let Err(e) = persist.remove(key) {
                                error!(message = "failed removing publication from store", err = %e);
                            }
                        }
                    }
                }
            }

            debug!("pumps for {} bridge stopped...", bridge_name);
        };

        // incoming pump
        let bridge_name = self.bridge_name.clone();
        let pump_type = self.pump_type.clone();
        let f2 = async move {
            debug!(
                "{} bridge starting ingress publication processing for pump {:?}...",
                bridge_name, pump_type
            );
            self.client.handle_events().await;
        };

        let has_receiver = receiver.is_some();

        let f3 = async move {
            if let Some(r) = receiver {
                while let Some(message) = r.recv().await {
                    match message {
                        BridgeMessage::ConnectivityUpdate(connectivity) => {
                            debug!("Received connectivity event {:?}", connectivity);
                            let payload: bytes::Bytes = connectivity.to_string().into();

                            let publication = Publication {
                                topic_name: CONNECTIVITY_TOPIC.to_owned(),
                                qos: AtLeastOnce,
                                retain: true,
                                payload,
                            };

                            if let Err(e) = connectivity_publish_handle.publish(publication).await {
                                error!("Failed to send connectivity event message {:?}", e);
                            }
                        }
                        _ => {} //Ignore other messages
                    }
                }
            }
        };

        let f1 = f1.fuse();
        let f2 = f2.fuse();
        let f3 = f3.fuse();
        let mut shutdown = shutdown.fuse();
        pin_mut!(f1);
        pin_mut!(f2);
        pin_mut!(f3);

        loop {
            select! {
                _ = f1 => {
                    error!(message = "publish loop failed and exited for bridge pump");
                    break;
                },
                _ = f2 => {
                    error!(message = "incoming message loop failed and exited for bridge pump");
                    break;
                },
                _ = f3 => {
                    // if it has a receiver and the future finished there must be an error
                    // if there was no receiver set then the future is finished right away, it should continue to loop for other futures to finish
                    if has_receiver {
                        error!(message = "incoming connectivity state loop failed and exited for bridge pump");
                        break;
                    }
                    else
                    {
                        debug!(message = "no connectivity receiver");
                    }
                },
                _ = shutdown => {
                    if let Err(e) = client_shutdown.shutdown().await {
                        error!(message = "failed to shutdown incoming message loop for bridge pump", err = %e);
                    }

                    if let Err(e) = loader_shutdown.send(()) {
                        error!(message = "failed to shutdown publish loop for bridge pump");
                    }

                    break;
                },
                complete => break
            };
        }

        f1.await;
        f2.await;
        f3.await;
    }
}
