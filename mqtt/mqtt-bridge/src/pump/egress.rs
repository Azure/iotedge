use futures_util::{pin_mut, stream::StreamExt};
use tokio::{select, sync::oneshot};
use tracing::{debug, error, info};

use crate::persist::{Key, PublicationStore, StreamWakeableState};

// Import and use mocks when run tests, real implementation when otherwise
#[cfg(test)]
pub use crate::client::MockPublishHandle as PublishHandle;

#[cfg(not(test))]
use crate::client::PublishHandle;

use mqtt3::proto::Publication;

const MAX_IN_FLIGHT: usize = 16;

/// Handles the egress of received publications.
///
/// It loads messages from the local store and publishes them as MQTT messages
/// to the broker. After acknowledgement is received from the broker it
/// deletes publication from the store.
pub(crate) struct Egress<S> {
    publish_handle: PublishHandle,
    store: PublicationStore<S>,
    shutdown_send: Option<oneshot::Sender<()>>,
    shutdown_recv: oneshot::Receiver<()>,
}

impl<S> Egress<S>
where
    S: StreamWakeableState,
{
    /// Creates a new instance of egress.
    pub(crate) fn new(publish_handle: PublishHandle, store: PublicationStore<S>) -> Egress<S> {
        let (shutdown_send, shutdown_recv) = oneshot::channel();

        Self {
            publish_handle,
            store,
            shutdown_send: Some(shutdown_send),
            shutdown_recv,
        }
    }

    /// Returns a shutdown handle of egress.
    pub(crate) fn handle(&mut self) -> EgressShutdownHandle {
        EgressShutdownHandle(self.shutdown_send.take())
    }

    /// Runs egress processing.
    pub(crate) async fn run(self) -> Result<(), EgressError> {
        let Egress {
            publish_handle,
            store,
            mut shutdown_recv,
            ..
        } = self;

        info!("starting egress publication processing...");

        // Take the stream of loaded messages and convert to a stream of futures
        // which publish. Then convert to buffered stream so that we can have
        // multiple in-flight and also limit number of publications.
        let publications = store
            .loader()
            .filter_map(|loaded| {
                let publish_handle = publish_handle.clone();
                async {
                    match loaded {
                        Ok((key, publication)) => {
                            Some(try_publish(key, publication, publish_handle))
                        }
                        Err(e) => {
                            error!(error = %e, "failed loading publication from store");
                            None
                        }
                    }
                }
            })
            .buffered(MAX_IN_FLIGHT)
            .fuse();

        pin_mut!(publications);

        loop {
            select! {
                _ = &mut shutdown_recv => {
                    debug!("received shutdown signal for egress messages");
                    break;
                }
                key = publications.select_next_some() => {
                    store.remove(key).map_err(|e| EgressError::RemovePublication(key, e))?;
                }
            }
        }

        info!("egress publication processing stopped");
        Ok(())
    }
}

async fn try_publish(key: Key, publication: Publication, mut publish_handle: PublishHandle) -> Key {
    debug!("forwarding publication with key {}", key);
    if let Err(e) = publish_handle.publish(publication).await {
        error!(error = %e, "failed forwarding publication");
    }

    key
}

/// Egress shutdown handle.
pub(crate) struct EgressShutdownHandle(Option<oneshot::Sender<()>>);

impl EgressShutdownHandle {
    /// Sends a signal to shutdown egress.
    pub(crate) async fn shutdown(mut self) {
        if let Some(sender) = self.0.take() {
            if sender.send(()).is_err() {
                error!("unable to request shutdown for egress.");
            }
        }
    }
}

#[derive(Debug, thiserror::Error)]
pub(crate) enum EgressError {
    #[error("Failed to remove publication from a store with key {0}. Caused by: {1}")]
    RemovePublication(Key, #[source] crate::persist::PersistError),
}
