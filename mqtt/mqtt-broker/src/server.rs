use std::{error::Error as StdError, fmt::Display, future::Future, net::ToSocketAddrs, sync::Arc};

use futures_util::{
    future::{self, Either, FutureExt},
    pin_mut,
    stream::StreamExt,
};
use tokio::sync::oneshot;
use tracing::{debug, error, info, info_span, warn};
use tracing_futures::Instrument;

use crate::{
    auth::{Authenticator, Authorizer, DynAuthenticator},
    broker::{Broker, BrokerHandle},
    connection::{self, MakeMqttPacketProcessor, MakePacketProcessor},
    transport::{GetPeerInfo, Transport},
    BrokerReadySignal, BrokerSnapshot, DetailedErrorValue, Error, InitializeBrokerError, Message,
    ServerCertificate, SystemEvent,
};

pub struct Server<Z, P> {
    broker: Broker<Z>,
    listeners: Vec<Listener>,
    make_processor: P,
}

impl<Z> Server<Z, MakeMqttPacketProcessor>
where
    Z: Authorizer + Send + 'static,
{
    pub fn from_broker(broker: Broker<Z>) -> Self {
        Self {
            broker,
            listeners: Vec::new(),
            make_processor: MakeMqttPacketProcessor,
        }
    }
}

impl<Z, P> Server<Z, P> {
    pub fn listeners(&self) -> &Vec<Listener> {
        &self.listeners
    }
}

impl<Z, P> Server<Z, P>
where
    Z: Authorizer + Send + 'static,
    P: MakePacketProcessor + Clone + Send + 'static,
{
    pub fn with_tcp<A, N, E>(
        &mut self,
        addr: A,
        authenticator: N,
        ready: Option<BrokerReadySignal>,
    ) -> Result<&mut Self, InitializeBrokerError>
    where
        A: ToSocketAddrs + Display,
        N: Authenticator<Error = E> + Send + Sync + 'static,
        E: StdError + Send + Sync + 'static,
    {
        let listener = Listener::new(
            Transport::new_tcp(addr)?,
            authenticator,
            self.broker.handle(),
            ready,
        );

        self.listeners.push(listener);
        Ok(self)
    }

    pub fn with_tls<A, N, E>(
        &mut self,
        addr: A,
        identity: ServerCertificate,
        authenticator: N,
        ready: Option<BrokerReadySignal>,
    ) -> Result<&mut Self, Error>
    where
        A: ToSocketAddrs + Display,
        N: Authenticator<Error = E> + Send + Sync + 'static,
        E: StdError + Send + Sync + 'static,
    {
        let listener = Listener::new(
            Transport::new_tls(addr, identity)?,
            authenticator,
            self.broker.handle(),
            ready,
        );

        self.listeners.push(listener);
        Ok(self)
    }

    pub fn with_packet_processor<P1>(self, make_processor: P1) -> Server<Z, P1> {
        Server {
            broker: self.broker,
            listeners: self.listeners,
            make_processor,
        }
    }

    pub async fn serve<F>(self, shutdown_signal: F) -> Result<BrokerSnapshot, Error>
    where
        F: Future<Output = ()>,
    {
        let Server {
            broker,
            listeners,
            make_processor,
        } = self;
        let handle = broker.handle();

        // prepare dispatcher in a separate task
        let broker_task = tokio::spawn(broker.run());
        pin_mut!(broker_task, shutdown_signal);

        // prepare each transport listener
        let mut incoming_tasks = Vec::new();
        let mut shutdown_handles = Vec::new();
        for listener in listeners {
            let (itx, irx) = oneshot::channel::<()>();
            shutdown_handles.push(itx);

            let incoming_task = Box::pin(listener.run(irx.map(drop), make_processor.clone()));
            incoming_tasks.push(incoming_task);
        }

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
                        handle.send(Message::System(SystemEvent::Shutdown))?;
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

                    handle.send(Message::System(SystemEvent::Shutdown))?;

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

pub struct Listener {
    transport: Transport,
    authenticator: Arc<(dyn Authenticator<Error = Box<dyn StdError + Send + Sync>> + Send + Sync)>,
    ready: Option<BrokerReadySignal>,
    broker_handle: BrokerHandle,
}

impl Listener {
    fn new<N, E>(
        transport: Transport,
        authenticator: N,
        broker_handle: BrokerHandle,
        ready: Option<BrokerReadySignal>,
    ) -> Self
    where
        N: Authenticator<Error = E> + Send + Sync + 'static,
        E: StdError + Into<Box<dyn StdError>> + Send + Sync + 'static,
    {
        let authenticator = DynAuthenticator::from(authenticator);
        Self {
            transport,
            authenticator: Arc::new(authenticator),
            ready,
            broker_handle,
        }
    }

    pub fn transport(&self) -> &Transport {
        &self.transport
    }

    async fn run<F, P>(self, shutdown_signal: F, make_processor: P) -> Result<(), Error>
    where
        F: Future<Output = ()> + Unpin,
        P: MakePacketProcessor + Clone + Send + 'static,
    {
        let Self {
            transport,
            authenticator,
            ready,
            broker_handle,
        } = self;

        let addr = transport.addr();

        let span = info_span!("server", listener=%addr);
        let inner_span = span.clone();

        async move {
            let ready = async {
                match ready {
                    Some(ready) => {
                        info!("waiting for broker to be ready to serve requests");
                        ready.wait().await
                    }
                    None => Ok(()),
                }
            };
            pin_mut!(ready);

            // wait until broker is ready to serve external clients or a shutdown request
            match future::select(ready, shutdown_signal).await {
                Either::Left((Ok(_), mut shutdown_signal)) => {
                    // start listening incoming connections on given network address
                    let mut incoming = transport.incoming().await?;

                    let addr = incoming.local_addr()?;
                    info!("listening on address {}", addr);

                    loop {
                        match future::select(&mut shutdown_signal, incoming.next()).await {
                            Either::Right((Some(Ok(stream)), _)) => {
                                let peer = stream.peer_addr()?;

                                let broker_handle = broker_handle.clone();
                                let span = inner_span.clone();
                                let authenticator = authenticator.clone();
                                let make_processor = make_processor.clone();

                                tokio::spawn(async move {
                                    if let Err(e) =
                                        connection::process(stream, peer, broker_handle, &*authenticator, make_processor)
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
                                    error =% DetailedErrorValue(&e),
                                    message = "accept loop exiting due to an error"
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
                Either::Left((Err(e), _)) => {
                    error!(error = %DetailedErrorValue(&e), "error occurred when waiting for broker readiness.");
                    Ok(())
                }
                Either::Right((_, _)) => {
                    info!("shutdown signalled while waiting for broker to be ready");
                    Ok(())
                }
            }
        }
        .instrument(span)
        .await
    }
}
