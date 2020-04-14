use std::future::Future;

use futures_util::future::{self, Either, FutureExt};
use futures_util::pin_mut;
use futures_util::stream::StreamExt;
use tokio::{net::ToSocketAddrs, sync::oneshot};
use tracing::{debug, error, info, span, warn, Level};
use tracing_futures::Instrument;

use crate::auth::{Authenticator, Authorizer};
use crate::broker::{Broker, BrokerHandle, BrokerState};
use crate::transport::TransportBuilder;
use crate::{connection, Error, InitializeBrokerError, Message, SystemEvent};

pub struct Server<N, Z>
where
    N: Authenticator,
    Z: Authorizer,
{
    broker: Broker<N, Z>,
}

impl<N, Z> Server<N, Z>
where
    N: Authenticator + Send + Sync + 'static,
    Z: Authorizer + Send + Sync + 'static,
{
    pub fn from_broker(broker: Broker<N, Z>) -> Self {
        Self { broker }
    }

    pub async fn serve<A, F, I>(
        self,
        transports: I,
        shutdown_signal: F,
    ) -> Result<BrokerState, Error>
    where
        A: ToSocketAddrs,
        F: Future<Output = ()> + Unpin,
        I: IntoIterator<Item = TransportBuilder<A>>,
    {
        let Server { broker } = self;
        let mut handle = broker.handle();
        let broker_task = tokio::spawn(broker.run());

        let mut incoming_tasks = Vec::new();
        let mut shutdown_handles = Vec::new();
        for transport in transports {
            let (itx, irx) = oneshot::channel::<()>();
            shutdown_handles.push(itx);

            let incoming_task = incoming_task(transport, handle.clone(), irx.map(drop));

            let incoming_task = Box::pin(incoming_task);
            incoming_tasks.push(incoming_task);
        }

        pin_mut!(broker_task);

        let incoming_tasks = future::select_all(incoming_tasks);
        let main_task = future::select(broker_task, incoming_tasks);

        // Handle shutdown
        let state = match future::select(shutdown_signal, main_task).await {
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
                            warn!(message = "failed to shutdown protocol head", error=%e);
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
                            warn!(message = "failed to shutdown protocol head", error=%e);
                        }

                        broker_state?
                    }
                }
            }
            Either::Right((either, _)) => match either {
                Either::Right(((result, index, unfinished_incoming_tasks), broker_task)) => {
                    debug!("sending Shutdown message to broker");

                    if let Err(e) = &result {
                        error!(message = "an error occurred in the accept loop", error=%e);
                    }

                    debug!("sending stop signal for the rest of protocol heads");
                    shutdown_handles.remove(index);
                    send_shutdown(shutdown_handles);

                    let mut results = vec![result];
                    results.extend(future::join_all(unfinished_incoming_tasks).await);

                    handle.send(Message::System(SystemEvent::Shutdown)).await?;

                    let broker_state = broker_task.await;

                    for e in results.into_iter().filter_map(Result::err) {
                        warn!(message = "failed to shutdown protocol head", error=%e);
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
                        warn!(message = "failed to shutdown protocol head", error=%e);
                    }

                    broker_state?
                }
            },
        };
        Ok(state)
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

async fn incoming_task<A, F>(
    transport: TransportBuilder<A>,
    handle: BrokerHandle,
    mut shutdown_signal: F,
) -> Result<(), Error>
where
    A: ToSocketAddrs,
    F: Future<Output = ()> + Unpin,
{
    let mut io = transport.build().await?;
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
                tokio::spawn(async move {
                    if let Err(e) = connection::process(stream, peer, broker_handle)
                        .instrument(span)
                        .await
                    {
                        warn!(message = "failed to process connection", error=%e);
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
                warn!("accept loop exiting due to an error - {}", e);
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
