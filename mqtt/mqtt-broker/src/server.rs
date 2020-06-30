use std::{error::Error as StdError, future::Future, sync::Arc};

use futures_util::{
    future::{self, Either, FutureExt},
    pin_mut,
    stream::StreamExt,
};
use tokio::sync::oneshot;
use tracing::{debug, error, info, span, warn, Level};
use tracing_futures::Instrument;

use mqtt_broker_core::auth::{Authenticator, Authorizer};

use crate::{
    broker::{Broker, BrokerHandle},
    connection,
    transport::TransportBuilder,
    BrokerSnapshot, DetailedErrorValue, Error, InitializeBrokerError, Message, SystemEvent,
};

pub struct Server<Z>
where
    Z: Authorizer,
{
    broker: Broker<Z>,
    #[allow(clippy::type_complexity)]
    transports: Vec<(
        TransportBuilder,
        Arc<(dyn Authenticator<Error = Box<dyn StdError>> + Send + Sync)>,
    )>,
}

impl<Z> Server<Z>
where
    Z: Authorizer + Send + 'static,
{
    pub fn from_broker(broker: Broker<Z>) -> Self {
        Self {
            broker,
            transports: Vec::default(),
        }
    }

    pub fn transport<N>(&mut self, new_transport: TransportBuilder, authenticator: N) -> &mut Self
    where
        N: Authenticator<Error = Box<dyn StdError>> + Send + Sync + 'static,
    {
        self.transports
            .push((new_transport, Arc::new(authenticator)));
        self
    }

    pub async fn serve<F>(self, shutdown_signal: F) -> Result<BrokerSnapshot, Error>
    where
        F: Future<Output = ()> + Unpin,
    {
        let Server { broker, transports } = self;
        let mut handle = broker.handle();
        let broker_task = tokio::spawn(broker.run());

        let mut incoming_tasks = Vec::new();
        let mut shutdown_handles = Vec::new();
        for (new_transport, authenticator) in transports {
            let (itx, irx) = oneshot::channel::<()>();
            shutdown_handles.push(itx);

            let incoming_task =
                incoming_task(new_transport, handle.clone(), irx.map(drop), authenticator);

            let incoming_task = Box::pin(incoming_task);
            incoming_tasks.push(incoming_task);
        }

        pin_mut!(broker_task);

        let incoming_tasks = future::select_all(incoming_tasks);
        let main_task = future::select(broker_task, incoming_tasks);

        // Handle shutdown
        match future::select(shutdown_signal, main_task).await {
            Either::Left((_, tasks)) => {
                info!("server received shutdown signal");

                // shutdown the incoming loop
                info!("shutting down accept loop...");

                debug!("sending stop signal for every protocol head");
                send_shutdown(shutdown_handles);

                match tasks.await {
                    Either::Right(((result, _index, unfinished_incoming_tasks), broker_task)) => {
                        // wait until the rest of incoming_tasks finished
                        let mut results = vec![result];
                        results.extend(future::join_all(unfinished_incoming_tasks).await);

                        for e in results.into_iter().filter_map(Result::err) {
                            warn!(message = "failed to shutdown protocol head", error =% DetailedErrorValue(&e));
                        }

                        debug!("sending Shutdown message to broker");
                        handle.send(Message::System(SystemEvent::Shutdown)).await?;
                        broker_task.await?
                    }
                    Either::Left((broker_state, incoming_tasks)) => {
                        warn!("broker exited before accept loop");

                        // wait until either of incoming_tasks finished
                        let (result, _index, unfinished_incoming_tasks) = incoming_tasks.await;

                        // wait until the rest of incoming_tasks finished
                        let mut results = vec![result];
                        results.extend(future::join_all(unfinished_incoming_tasks).await);

                        for e in results.into_iter().filter_map(Result::err) {
                            warn!(message = "failed to shutdown protocol head", error =% DetailedErrorValue(&e));
                        }

                        broker_state?
                    }
                }
            }
            Either::Right((either, _)) => match either {
                Either::Right(((result, index, unfinished_incoming_tasks), broker_task)) => {
                    if let Err(e) = &result {
                        error!(
                            message = "an error occurred in the accept loop",
                            error =% DetailedErrorValue(e)
                        );
                    }

                    debug!("sending stop signal for the rest of protocol heads");
                    shutdown_handles.remove(index);

                    debug!("sending Shutdown message to broker");
                    send_shutdown(shutdown_handles);

                    let results = future::join_all(unfinished_incoming_tasks).await;

                    handle.send(Message::System(SystemEvent::Shutdown)).await?;

                    let broker_state = broker_task.await;

                    for e in results.into_iter().filter_map(Result::err) {
                        warn!(message = "failed to shutdown protocol head", error =% DetailedErrorValue(&e));
                    }

                    broker_state?
                }
                Either::Left((broker_state, incoming_tasks)) => {
                    warn!("broker exited before accept loop");

                    debug!("sending stop signal for the rest of protocol heads");
                    send_shutdown(shutdown_handles);

                    // wait until either of incoming_tasks finished
                    let (result, _index, unfinished_incoming_tasks) = incoming_tasks.await;

                    // wait until the rest of incoming_tasks finished
                    let mut results = vec![result];
                    results.extend(future::join_all(unfinished_incoming_tasks).await);

                    for e in results.into_iter().filter_map(Result::err) {
                        warn!(message = "failed to shutdown protocol head", error =% DetailedErrorValue(&e));
                    }

                    broker_state?
                }
            },
        }
    }
}

fn send_shutdown<I>(handles: I)
where
    I: IntoIterator<Item = oneshot::Sender<()>>,
{
    for itx in handles {
        if let Err(()) = itx.send(()) {
            warn!(message = "failed to signal protocol head to stop");
        }
    }
}

async fn incoming_task<F, N>(
    new_transport: TransportBuilder,
    handle: BrokerHandle,
    mut shutdown_signal: F,
    authenticator: Arc<N>,
) -> Result<(), Error>
where
    F: Future<Output = ()> + Unpin,
    N: Authenticator + ?Sized + Send + Sync + 'static,
{
    let io = new_transport.make_transport().await?;
    let addr = io.local_addr()?;
    let span = span!(Level::INFO, "server", listener=%addr);
    let _enter = span.enter();

    let mut incoming = io.incoming();

    info!("Listening on address {}", addr);

    loop {
        match future::select(&mut shutdown_signal, incoming.next()).await {
            Either::Right((Some(Ok(stream)), _)) => {
                let peer = stream
                    .peer_addr()
                    .map_err(InitializeBrokerError::ConnectionPeerAddress)?;

                let broker_handle = handle.clone();
                let span = span.clone();
                let authenticator = authenticator.clone();
                tokio::spawn(async move {
                    if let Err(e) =
                        connection::process(stream, peer, broker_handle, &*authenticator)
                            .instrument(span)
                            .await
                    {
                        warn!(message = "failed to process connection", error =% DetailedErrorValue(&e));
                    }
                });
            }
            Either::Left(_) => {
                info!(
                    "accept loop shutdown. no longer accepting connections on {}",
                    addr
                );
                break;
            }
            Either::Right((Some(Err(e)), _)) => {
                warn!(
                    "accept loop exiting due to an error - {}",
                    DetailedErrorValue(&e)
                );
                break;
            }
            Either::Right((None, _)) => {
                warn!("accept loop exiting due to no more incoming connections (incoming returned None)");
                break;
            }
        }
    }
    Ok(())
}
