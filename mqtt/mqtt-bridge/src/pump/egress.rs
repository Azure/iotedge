use futures_util::{
    stream::{FuturesUnordered, StreamExt, TryStreamExt},
    FutureExt,
};
use tokio::{select, sync::oneshot};
use tracing::{debug, error, info};

use crate::client::InFlightPublishHandle;
use crate::persist::{PublicationStore, StreamWakeableState};

// Import and use mocks when run tests, real implementation when otherwise
#[cfg(test)]
pub use crate::client::MockallPublishHandleWrapper as PublishHandle;

#[cfg(not(test))]
use crate::client::PublishHandle;

/// Handles the egress of received publications.
///
/// It loads messages from the local store and publishes them as MQTT messages
/// to the broker. After acknowledgement is received from the broker it
/// deletes publication from the store.
pub(crate) struct Egress<S> {
    publish_handle: InFlightPublishHandle<PublishHandle>,
    store: PublicationStore<S>,
    shutdown_send: Option<oneshot::Sender<()>>,
    shutdown_recv: oneshot::Receiver<()>,
}

impl<S> Egress<S>
where
    S: StreamWakeableState,
{
    /// Creates a new instance of egress.
    pub(crate) fn new(
        publish_handle: InFlightPublishHandle<PublishHandle>,
        store: PublicationStore<S>,
    ) -> Egress<S> {
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
    pub(crate) async fn run(self) {
        let Egress {
            publish_handle,
            store,
            shutdown_recv,
            ..
        } = self;

        info!("starting egress publication processing...");

        let mut shutdown = shutdown_recv.fuse();
        let mut loader = store.loader().fuse();
        let mut in_flight_publishes = FuturesUnordered::new();

        loop {
            select! {
                _ = &mut shutdown => {
                    info!("received shutdown signal for egress messages");
                    break;
                }
                maybe_publication = loader.try_next() => {
                    debug!("extracted publication from store");

                    if let Ok(Some((key, publication))) = maybe_publication {
                        let store = store.clone();
                        let publish_fut = publish_handle.publish_future(publication).await;

                        debug!("scheduling publish for publication {:?}", key);
                        let publish_and_remove = async move {
                            debug!("publishing publication {:?}", key);
                            if let Err(e) = publish_fut.await {
                                error!(err = %e, "failed sending publication {:?}", key);
                            }

                            if let Err(e) = store.remove(key) {
                                error!(err = %e, "failed removing publication from store {:?}", key);
                            }
                        };

                        in_flight_publishes.push(publish_and_remove);
                    }
                }
                _ = in_flight_publishes.next() => {}
            }
        }

        info!("finished egress publication processing");
    }
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
