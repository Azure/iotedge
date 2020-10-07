#![allow(dead_code)]
use std::{cell::RefCell, rc::Rc};

use futures_util::{
    future::{select, Either, FutureExt},
    pin_mut, select,
    stream::{StreamExt, TryStreamExt},
};
use tokio::sync::{oneshot, oneshot::Receiver};
use tracing::debug;
use tracing::error;

use mqtt3::PublishHandle;

use crate::{
    bridge::BridgeError,
    client::{ClientShutdownHandle, MqttClient},
    message_handler::MessageHandler,
    persist::{MessageLoader, PublicationStore, WakingMemoryStore},
};

pub struct Pump {
    client: MqttClient<MessageHandler<WakingMemoryStore>>,
    client_shutdown: ClientShutdownHandle,
    publish_handle: PublishHandle,
    subscriptions: Vec<String>,
    loader: Rc<RefCell<MessageLoader<WakingMemoryStore>>>,
    persist: Rc<RefCell<PublicationStore<WakingMemoryStore>>>,
}

impl Pump {
    pub fn new(
        client: MqttClient<MessageHandler<WakingMemoryStore>>,
        subscriptions: Vec<String>,
        loader: Rc<RefCell<MessageLoader<WakingMemoryStore>>>,
        egress_persist: Rc<RefCell<PublicationStore<WakingMemoryStore>>>,
    ) -> Result<Self, BridgeError> {
        let publish_handle = client
            .publish_handle()
            .map_err(BridgeError::PublishHandle)?;
        let client_shutdown = client.shutdown_handle()?;

        Ok(Self {
            client,
            client_shutdown,
            publish_handle: publish_handle,
            subscriptions,
            loader,
            persist: egress_persist,
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
        let publish_handle = self.publish_handle.clone();
        let loader = self.loader.clone();
        let persist = self.persist.clone();
        let mut client_shutdown = self.client_shutdown.clone();

        let f1 = async move {
            // TODO PRE: move this to init somehow
            let mut loader_borrow = loader.borrow_mut();
            let mut receive_fut = loader_shutdown_rx.into_stream();

            debug!("started outgoing pump");

            loop {
                let mut publish_handle = publish_handle.clone();
                match select(receive_fut.next(), loader_borrow.try_next()).await {
                    Either::Left((shutdown, _)) => {
                        debug!("outgoing pump received shutdown signal");
                        if let None = shutdown {
                            error!(message = "unexpected behavior from shutdown signal while signalling bridge pump shutdown")
                        }

                        debug!("bridge pump stopped");
                        break;
                    }
                    Either::Right((p, _)) => {
                        debug!("outgoing pump extracted publication from store");

                        // TODO_PRE: handle publication error
                        let (key, publication) = p.unwrap().unwrap();

                        // TODO PRE: should we be retrying?
                        // if this failure is due to something that will keep failing it is probably safer to remove and never try again
                        // otherwise we should retry
                        debug!("publishing publication {:?} for outgoing pump", key);
                        if let Err(e) = publish_handle.publish(publication).await {
                            error!(message = "failed publishing publication for bridge pump", err = %e);
                        }

                        let persist_borrow = persist.try_borrow_mut();
                        match persist_borrow {
                            Ok(mut persist_borrow) => {
                                // TODO PRE: if removal fails, what do we do?
                                if let Err(e) = persist_borrow.remove(key) {
                                    error!(message = "failed removing publication from store", err = %e);
                                }
                                drop(persist_borrow);
                            }
                            Err(e) => {
                                error!(message = "failed to borrow persistence to remove publication", err = %e);
                            }
                        }
                    }
                }
            }
        };

        let f2 = self.client.handle_events();

        let f1 = f1.fuse();
        let f2 = f2.fuse();
        let mut shutdown = shutdown.fuse();
        pin_mut!(f1);
        pin_mut!(f2);

        select! {
            _ = f1 => {
                error!(message = "publish loop failed and exited for bridge pump");
            },
            _ = f2 => {
                error!(message = "incoming publication loop failed and exited for bridge pump");
            },
            _ = shutdown => {
                if let Err(e) = client_shutdown.shutdown().await {
                    error!(message = "failed to shutdown incoming publication loop for bridge pump", err = %e);
                }

                if let Err(e) = loader_shutdown.send(()) {
                    error!(message = "failed to shutdown publish loop for bridge pump");
                }
            },
        }

        f1.await;
        f2.await;
    }
}
