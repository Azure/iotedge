#![allow(dead_code)]
use std::{cell::RefCell, rc::Rc, sync::Arc};

use futures_util::{
    future::{select, Either, FutureExt},
    pin_mut,
    select,
    // stream::{FuturesUnordered, StreamExt, TryStreamExt},
    stream::{StreamExt, TryStreamExt},
};
use tokio::sync::{mpsc::UnboundedReceiver, oneshot, oneshot::Receiver, Mutex};
use tracing::debug;
use tracing::error;

use mqtt3::{proto::Publication, proto::QoS::AtLeastOnce, PublishHandle};

use crate::{
    bridge::BridgeError,
    bridge::BridgeMessage,
    client::{ClientShutdownHandle, MqttClient},
    message_handler::MessageHandler,
    persist::{MessageLoader, PublicationStore, WakingMemoryStore},
};

//const MAX_INFLIGHT: usize = 16;
const CONNECTIVITY_TOPIC: &str = "$sys/connectivity";

// TODO PRE: make this generic
pub struct Pump {
    client: MqttClient<MessageHandler<WakingMemoryStore>>,
    client_shutdown: ClientShutdownHandle,
    publish_handle: PublishHandle,
    subscriptions: Vec<String>,
    loader: Arc<Mutex<MessageLoader<WakingMemoryStore>>>,
    persist: Rc<RefCell<PublicationStore<WakingMemoryStore>>>,
    connectivity_receiver: Option<UnboundedReceiver<BridgeMessage>>,
}

impl Pump {
    pub fn new(
        client: MqttClient<MessageHandler<WakingMemoryStore>>,
        subscriptions: Vec<String>,
        loader: Arc<Mutex<MessageLoader<WakingMemoryStore>>>,
        persist: Rc<RefCell<PublicationStore<WakingMemoryStore>>>,
        connectivity_receiver: Option<UnboundedReceiver<BridgeMessage>>,
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
        let (loader_shutdown, loader_shutdown_rx) = oneshot::channel::<()>();
        // let mut senders = FuturesUnordered::new();
        let publish_handle = self.publish_handle.clone();
        let mut connectivity_publish_handle = self.publish_handle.clone();
        let loader = self.loader.clone();
        // let persist = self.persist.clone();
        let mut client_shutdown = self.client_shutdown.clone();
        let receiver = self.connectivity_receiver.as_mut();

        let f1 = async move {
            let mut loader_lock = loader.lock().await;
            let mut receive_fut = loader_shutdown_rx.into_stream();

            debug!("started outgoing pump");

            loop {
                let mut publish_handle = publish_handle.clone();
                match select(receive_fut.next(), loader_lock.try_next()).await {
                    Either::Left((shutdown, _)) => {
                        debug!("outgoing pump received shutdown signal");
                        if let None = shutdown {
                            error!(message = "unexpected behavior from shutdown signal while signalling bridge pump shutdown")
                        }

                        // debug!("waiting on all remaining in-flight messages to send");
                        // for sender in senders.iter_mut() {
                        //     sender.await;
                        // }

                        debug!("all messages sent for outgoing pump");
                        break;
                    }
                    Either::Right((p, _)) => {
                        debug!("outgoing pump extracted message from store");

                        // TODO_PRE: handle publication error
                        let p = p.unwrap().unwrap();

                        debug!("publishing message {:?} for outgoing pump", p.0);
                        // let persist_copy = persist.clone();
                        let fut = async move {
                            if let Err(e) = publish_handle.publish(p.1).await {
                                error!(message = "failed publishing message for bridge pump", err = %e);
                            } else {
                                // TODO PRE: should we be retrying?
                                // if this failure is due to something that will keep failing it is probably safer to remove and never try again

                                // TODO PRE: We need to remove from the other persistor
                                // let mut persist = persist_copy.borrow_mut();
                                // if let Err(e) = persist.remove(p.0) {
                                //     error!(message = "failed to remove message from store for bridge pump", err = %e);
                                // }
                                // drop(persist);
                            }
                        };

                        fut.await;

                        // if senders.len() < MAX_INFLIGHT {
                        //     debug!("publishing message for outgoing pump");
                        //     let persist_copy = persist.clone();
                        //     let fut = async move {
                        //         let mut persist = persist_copy.borrow_mut();
                        //         if let Err(e) = publish_handle.publish(p.1).await {
                        //             error!(message = "failed publishing message for bridge pump", err = %e);
                        //         } else {
                        //             // TODO PRE: should we be retrying?
                        //             // if this failure is due to something that will keep failing it is probably safer to remove and never try again
                        //             if let Err(e) = persist.remove(p.0) {
                        //                 error!(message = "failed to remove message from store for bridge pump", err = %e);
                        //             }
                        //         }
                        //     };
                        //     senders.push(Box::pin(fut));
                        // } else {
                        //     debug!("outgoing pump max in-flight messages reached");
                        //     senders.next().await;
                        // }
                    }
                }
            }
        };

        let f2 = self.client.handle_events();

        let f3 = async move {
            //let mut publish = publish_handle.clone();
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

        select! {
            _ = f1 => {
                error!(message = "publish loop failed and exited for bridge pump");
            },
            _ = f2 => {
                error!(message = "incoming message loop failed and exited for bridge pump");
            },
            _ = f3 => {
                error!(message = "incoming connectivity state loop failed and exited for bridge pump");
            },
            _ = shutdown => {
                if let Err(e) = client_shutdown.shutdown().await {
                    error!(message = "failed to shutdown incoming message loop for bridge pump", err = %e);
                }

                if let Err(e) = loader_shutdown.send(()) {
                    error!(message = "failed to shutdown publish loop for bridge pump");
                }
            },
        }

        f1.await;
        f2.await;
        f3.await;
    }
}
