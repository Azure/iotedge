#![allow(unused_imports)] // TODO: Remove when ready
#![allow(dead_code)] // TODO: Remove when ready
#![allow(unused_variables)] // TODO: Remove when ready

use std::cell::RefCell;
use std::rc::Rc;
use std::{collections::HashMap, convert::TryFrom, convert::TryInto, sync::Arc, time::Duration};

use async_trait::async_trait;
use futures_util::future::select;
use futures_util::future::select_all;
use futures_util::future::Either;
use futures_util::future::FutureExt;
use futures_util::pin_mut;
use futures_util::select;
use futures_util::stream::FuturesUnordered;
use futures_util::stream::StreamExt;
use futures_util::stream::TryStreamExt;
use mqtt3::{proto::Publication, Event, PublishHandle, ReceivedPublication, ShutdownError};
use mqtt_broker::TopicFilter;
use tokio::sync::oneshot;
use tokio::sync::oneshot::channel;
use tokio::sync::oneshot::Receiver;
use tokio::sync::oneshot::Sender;
use tokio::sync::Mutex;
use tokio::time;
use tracing::error;
use tracing::{debug, info, warn};

use crate::{
    client::{ClientError, ClientShutdownHandle, EventHandler, MqttClient},
    message_handler::MessageHandler,
    persist::{
        MessageLoader, PersistError, PublicationStore, StreamWakeableState, WakingMemoryStore,
    },
    settings::{ConnectionSettings, Credentials, TopicRule},
};

const MAX_INFLIGHT: usize = 16;

#[derive(Debug, thiserror::Error)]
pub enum PumpError {
    #[error("Failed to get publish handle from client.")]
    PublishHandle(#[from] ClientError),

    #[error("Failed to get publish handle from client.")]
    ClientShutdown(#[from] ShutdownError),
}

// TODO PRE: make this generic
pub struct Pump {
    client: MqttClient<MessageHandler<WakingMemoryStore>>,
    client_shutdown: ClientShutdownHandle,
    publish_handle: PublishHandle,
    subscriptions: Vec<String>,
    loader: Arc<Mutex<MessageLoader<WakingMemoryStore>>>,
    persist: Rc<RefCell<PublicationStore<WakingMemoryStore>>>,
}

impl Pump {
    pub fn new(
        client: MqttClient<MessageHandler<WakingMemoryStore>>,
        subscriptions: Vec<String>,
        loader: Arc<Mutex<MessageLoader<WakingMemoryStore>>>,
        persist: Rc<RefCell<PublicationStore<WakingMemoryStore>>>,
    ) -> Result<Self, PumpError> {
        let publish_handle = client.publish_handle()?;
        let client_shutdown = client.shutdown_handle()?;

        Ok(Self {
            client,
            client_shutdown,
            publish_handle: publish_handle,
            subscriptions,
            loader,
            persist,
        })
    }

    pub async fn run(&mut self, shutdown: Receiver<()>) {
        let (loader_shutdown, loader_shutdown_rx) = oneshot::channel::<()>();
        let mut senders = FuturesUnordered::new();
        let mut publish_handle = self.publish_handle.clone();
        let loader = self.loader.clone();
        let persist = self.persist.clone();
        let mut client_shutdown = self.client_shutdown.clone();
        let f1 = async move {
            let mut loader_lock = loader.lock().await;
            match select(loader_shutdown_rx, loader_lock.try_next()).await {
                Either::Left((shutdown, loader_next)) => {
                    for sender in senders.iter_mut() {
                        sender.await;
                    }
                }
                Either::Right((p, _)) => {
                    // TODO_PRE: handle publication error
                    let p = p.unwrap().unwrap();

                    // TODO PRE: handle error

                    // send pubs if there is a spots in the inflight queue
                    if senders.len() < MAX_INFLIGHT {
                        let persist_copy = persist.clone();
                        let fut = async move {
                            let mut persist = persist_copy.borrow_mut();
                            // TODO PRE: log error if this fails
                            if let Err(e) = publish_handle.publish(p.1).await {
                                // TODO PRE: say which bridge pump
                                error!(message = "failed publishing message for bridge pump", err = %e);
                            } else {
                                // TODO PRE: should we be retrying?
                                // if this failure is due to something that will keep failing it is probably safer to remove and never try again
                                if let Err(e) = persist.remove(p.0) {
                                    // TODO PRE: give context to error
                                    error!(message = "failed to remove message from store for bridge pump", err = %e);
                                }
                            }
                        };
                        senders.push(Box::pin(fut));
                    } else {
                        senders.next().await;
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
                error!(message = "incoming message loop failed and exited for bridge pump");
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
    }
}
