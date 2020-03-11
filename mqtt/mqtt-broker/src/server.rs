use std::fmt::Display;
use std::future::Future;

use failure::ResultExt;

use futures_util::future::{self, Either, FutureExt};
use futures_util::pin_mut;
use futures_util::stream::StreamExt;
use tokio::net::{TcpListener, ToSocketAddrs};
use tokio::sync::oneshot;
use tracing::{debug, error, info, span, warn, Level};
use tracing_futures::Instrument;

use crate::broker::{Broker, BrokerHandle, BrokerState};
use crate::{connection, Error, ErrorKind, Message, SystemEvent};

#[derive(Default)]
pub struct Server {
    broker: Broker,
}

impl Server {
    pub fn new() -> Self {
        Self::from_broker(Broker::default())
    }

    pub fn from_broker(broker: Broker) -> Self {
        Self { broker }
    }

    pub async fn serve<A, F>(self, addr: A, shutdown_signal: F) -> Result<BrokerState, Error>
    where
        A: ToSocketAddrs + Display,
        F: Future<Output = ()> + Unpin,
    {
        let Server { broker } = self;
        let mut handle = broker.handle();

        let (itx, irx) = oneshot::channel::<()>();

        let broker_task = tokio::spawn(broker.run());
        let incoming_task = incoming_task(addr, handle.clone(), irx.map(drop));
        pin_mut!(broker_task);
        pin_mut!(incoming_task);

        let main_task = future::select(broker_task, incoming_task);

        // Handle shutdown
        let state = match future::select(shutdown_signal, main_task).await {
            Either::Left((_, tasks)) => {
                info!("server received shutdown signal");

                // shutdown the incoming loop
                info!("shutting down accept loop...");
                itx.send(()).unwrap();
                match tasks.await {
                    Either::Right((_, broker_task)) => {
                        debug!("sending Shutdown message to broker");
                        handle.send(Message::System(SystemEvent::Shutdown)).await?;
                        broker_task.await.context(ErrorKind::BrokerJoin)?
                    }
                    Either::Left((broker_state, incoming_task)) => {
                        warn!("broker exited before accept loop");
                        incoming_task.await?;
                        broker_state.context(ErrorKind::BrokerJoin)?
                    }
                }
            }
            Either::Right((either, _shutdown)) => match either {
                Either::Right((Ok(_incoming_task), broker_task)) => {
                    debug!("sending Shutdown message to broker");
                    handle.send(Message::System(SystemEvent::Shutdown)).await?;
                    broker_task.await.context(ErrorKind::BrokerJoin)?
                }
                Either::Right((Err(e), broker_task)) => {
                    error!(message = "an error occurred in the accept loop", error=%e);
                    debug!("sending Shutdown message to broker");
                    handle.send(Message::System(SystemEvent::Shutdown)).await?;
                    broker_task.await.context(ErrorKind::BrokerJoin)?;
                    return Err(e);
                }
                Either::Left((broker_state, incoming_task)) => {
                    warn!("broker exited before accept loop");
                    incoming_task.await?;
                    broker_state.context(ErrorKind::BrokerJoin)?
                }
            },
        };
        Ok(state)
    }
}

async fn incoming_task<A, F>(
    addr: A,
    handle: BrokerHandle,
    mut shutdown_signal: F,
) -> Result<(), Error>
where
    A: ToSocketAddrs + Display,
    F: Future<Output = ()> + Unpin,
{
    let span = span!(Level::INFO, "server", listener=%addr);
    let _enter = span.enter();

    let mut listener = TcpListener::bind(&addr)
        .await
        .context(ErrorKind::BindServer)?;
    let mut incoming = listener.incoming();

    info!("Listening on address {}", addr);

    loop {
        match future::select(&mut shutdown_signal, incoming.next()).await {
            Either::Right((Some(Ok(stream)), _)) => {
                stream
                    .set_nodelay(true)
                    .context(ErrorKind::ConnectionConfiguration)?;
                let peer = stream
                    .peer_addr()
                    .context(ErrorKind::ConnectionPeerAddress)?;

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
