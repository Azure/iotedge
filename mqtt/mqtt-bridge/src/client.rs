#![allow(dead_code)] // TODO remove when ready
use std::{
    collections::HashSet,
    fmt::Display,
    io::Error,
    io::ErrorKind,
    pin::Pin,
    str,
    sync::atomic::{AtomicU8, Ordering},
    sync::Arc,
    time::Duration,
};

use async_trait::async_trait;
use chrono::Utc;
use futures_util::future::{self, BoxFuture};
use openssl::{ssl::SslConnector, ssl::SslMethod, x509::X509};
use tokio::{
    io::AsyncRead,
    io::AsyncWrite,
    net::TcpStream,
    stream::StreamExt,
    sync::{mpsc::Receiver, Mutex, Semaphore},
};
use tracing::{debug, error, info, warn};

use mqtt3::PublishHandle;
use mqtt3::{
    proto::{self, Publication},
    Client, Event, IoSource, PublishError, ShutdownError, SubscriptionUpdateEvent,
    UpdateSubscriptionError,
};

use crate::{
    settings::Credentials,
    token_source::{SasTokenSource, TokenSource, TrustBundleSource},
};

const DEFAULT_TOKEN_DURATION_MINS: i64 = 60;
const DEFAULT_MAX_RECONNECT: Duration = Duration::from_secs(5);
// TODO: get QOS from topic settings
const DEFAULT_QOS: proto::QoS = proto::QoS::AtLeastOnce;
const API_VERSION: &str = "2010-01-01";

#[derive(Debug, Clone)]
pub struct ClientShutdownHandle(mqtt3::ShutdownHandle);

impl ClientShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), ClientError> {
        self.0
            .shutdown()
            .await
            .map_err(ClientError::ShutdownClient)?;
        Ok(())
    }
}

#[derive(Clone)]
enum BridgeIoSource {
    Tcp(TcpConnection<SasTokenSource>),
    Tls(TcpConnection<SasTokenSource>),
}

trait BridgeIo: AsyncRead + AsyncWrite + Send + Sync + 'static {}

impl<I> BridgeIo for I where I: AsyncRead + AsyncWrite + Send + Sync + 'static {}

type BridgeIoSourceFuture =
    BoxFuture<'static, Result<(Pin<Box<dyn BridgeIo>>, Option<String>), Error>>;

#[derive(Clone)]
pub struct TcpConnection<T>
where
    T: TokenSource + Clone + Send + Sync + 'static,
{
    address: String,
    token_source: Option<T>,
    trust_bundle_source: Option<TrustBundleSource>,
}

impl<T> TcpConnection<T>
where
    T: TokenSource + Clone + Send + Sync + 'static,
{
    pub fn new(
        address: String,
        token_source: Option<T>,
        trust_bundle_source: Option<TrustBundleSource>,
    ) -> Self {
        Self {
            address,
            token_source,
            trust_bundle_source,
        }
    }
}

impl IoSource for BridgeIoSource {
    type Io = Pin<Box<dyn BridgeIo>>;

    type Error = Error;

    #[allow(clippy::type_complexity)]
    type Future = BoxFuture<'static, Result<(Self::Io, Option<String>), Self::Error>>;

    fn connect(&mut self) -> Self::Future {
        match self {
            BridgeIoSource::Tcp(connect_settings) => Self::get_tcp_source(connect_settings.clone()),
            BridgeIoSource::Tls(connect_settings) => Self::get_tls_source(connect_settings.clone()),
        }
    }
}

impl BridgeIoSource {
    fn get_tcp_source(connection_settings: TcpConnection<SasTokenSource>) -> BridgeIoSourceFuture {
        let address = connection_settings.address;
        let token_source = connection_settings.token_source;

        Box::pin(async move {
            let expiry = Utc::now() + chrono::Duration::minutes(DEFAULT_TOKEN_DURATION_MINS);

            let io = TcpStream::connect(&address);

            let token_task = async {
                match token_source {
                    Some(ts) => ts.get(&expiry).await,
                    None => Ok(None),
                }
            };

            let (password, io) = future::try_join(token_task, io).await.map_err(|err| {
                Error::new(ErrorKind::Other, format!("failed to connect: {}", err))
            })?;

            let stream: Pin<Box<dyn BridgeIo>> = Box::pin(io);
            Ok((stream, password))
        })
    }

    fn get_tls_source(connection_settings: TcpConnection<SasTokenSource>) -> BridgeIoSourceFuture {
        let address = connection_settings.address.clone();
        let token_source = connection_settings.token_source.as_ref().cloned();
        let trust_bundle_source = connection_settings.trust_bundle_source;

        Box::pin(async move {
            let expiry = Utc::now() + chrono::Duration::minutes(DEFAULT_TOKEN_DURATION_MINS);

            let server_root_certificate_task = async {
                match trust_bundle_source {
                    Some(source) => source.get_trust_bundle().await,
                    None => Ok(None),
                }
            };

            let token_task = async {
                match token_source {
                    Some(ts) => ts.get(&expiry).await,
                    None => Ok(None),
                }
            };

            let io = TcpStream::connect(address.clone());

            let (server_root_certificate, password, stream) =
                future::try_join3(server_root_certificate_task, token_task, io)
                    .await
                    .map_err(|err| {
                        Error::new(ErrorKind::Other, format!("failed to connect: {}", err))
                    })?;

            let config = SslConnector::builder(SslMethod::tls())
                .map(|mut builder| {
                    if let Some(trust_bundle) = server_root_certificate {
                        X509::stack_from_pem(trust_bundle.as_bytes())
                            .map(|mut certs| {
                                while let Some(ca) = certs.pop() {
                                    builder.cert_store_mut().add_cert(ca).ok();
                                }
                            })
                            .ok();
                    }

                    builder.build()
                })
                .and_then(|conn| conn.configure())
                .map_err(|e| Error::new(ErrorKind::NotConnected, e))?;

            let hostname = address.split(':').next().unwrap_or(&address);

            let io = tokio_openssl::connect(config, &hostname, stream).await;

            debug!("Tls connection {:?} for {:?}", io, address);

            io.map(|io| {
                let stream: Pin<Box<dyn BridgeIo>> = Box::pin(io);
                Ok((stream, password))
            })
            .map_err(|e| Error::new(ErrorKind::NotConnected, e))?
        })
    }
}

/// Trait used to facilitate mock publish handle types
#[async_trait]
pub trait InnerPublishHandle {
    async fn publish(&mut self, publication: Publication) -> Result<(), PublishError>;
}

#[derive(Debug, Clone)]
pub struct ClientPublishHandle(PublishHandle);

#[async_trait]
impl InnerPublishHandle for ClientPublishHandle {
    async fn publish(&mut self, publication: Publication) -> Result<(), PublishError> {
        self.0.publish(publication).await
    }
}

/// Mock publish handle mimicking the mqtt3 publish handle.
/// Used for tests only.
/// This publish handle needs to implement clone which necessitates Arc and Mutex
//  but will never be sent between threads.
#[derive(Debug, Clone)]
pub struct MockPublishHandle {
    counter: Arc<AtomicU8>,
    send_trigger: Arc<Mutex<Receiver<()>>>,
}

impl MockPublishHandle {
    fn new(counter: Arc<AtomicU8>, send_trigger: Arc<Mutex<Receiver<()>>>) -> Self {
        Self {
            counter,
            send_trigger,
        }
    }
}

#[async_trait]
impl InnerPublishHandle for MockPublishHandle {
    async fn publish(&mut self, _: Publication) -> Result<(), PublishError> {
        self.counter.fetch_add(1, Ordering::Relaxed);
        self.send_trigger.lock().await.next().await;
        Ok(())
    }
}

#[derive(Debug, Clone)]
pub struct InFlightPublishHandle<P> {
    publish_handle: P,
    permits: Arc<Semaphore>,
}

impl<P> InFlightPublishHandle<P>
where
    P: InnerPublishHandle + Send + Clone + 'static,
{
    fn new(publish_handle: P, permits: Arc<Semaphore>) -> Self {
        Self {
            publish_handle,
            permits,
        }
    }

    pub async fn publish_future(&self, publication: Publication) -> BoxFuture<'static, ()> {
        let permits = self.permits.clone();
        let permit = permits.acquire_owned().await;
        let mut publish_handle = self.publish_handle.clone();

        let publication_send = async move {
            if let Err(e) = publish_handle.publish(publication).await {
                error!(message = "failed to publish", err = %e);
            }

            drop(permit);
        };

        Box::pin(publication_send)
    }
}

/// This is a wrapper over mqtt3 client
pub struct MqttClient<H>
where
    H: EventHandler,
{
    client_id: Option<String>,
    username: Option<String>,
    io_source: BridgeIoSource,
    keep_alive: Duration,
    client: Client<BridgeIoSource>,
    event_handler: H,
    in_flight_handle: InFlightPublishHandle<ClientPublishHandle>,
}

impl<H: EventHandler> MqttClient<H> {
    pub fn tcp(
        address: &str,
        keep_alive: Duration,
        clean_session: bool,
        event_handler: H,
        connection_credentials: &Credentials,
        max_in_flight: usize,
    ) -> Result<Self, ClientError> {
        let token_source = Self::token_source(&connection_credentials);
        let tcp_connection = TcpConnection::new(address.to_owned(), token_source, None);
        let io_source = BridgeIoSource::Tcp(tcp_connection);

        Self::new(
            keep_alive,
            clean_session,
            event_handler,
            connection_credentials,
            io_source,
            max_in_flight,
        )
    }

    pub fn tls(
        address: &str,
        keep_alive: Duration,
        clean_session: bool,
        event_handler: H,
        connection_credentials: &Credentials,
        max_in_flight: usize,
    ) -> Result<Self, ClientError> {
        let trust_bundle = Some(TrustBundleSource::new(connection_credentials.clone()));

        let token_source = Self::token_source(&connection_credentials);
        let tcp_connection = TcpConnection::new(address.to_owned(), token_source, trust_bundle);
        let io_source = BridgeIoSource::Tls(tcp_connection);

        Self::new(
            keep_alive,
            clean_session,
            event_handler,
            connection_credentials,
            io_source,
            max_in_flight,
        )
    }

    fn new(
        keep_alive: Duration,
        clean_session: bool,
        event_handler: H,
        connection_credentials: &Credentials,
        io_source: BridgeIoSource,
        max_in_flight: usize,
    ) -> Result<Self, ClientError> {
        let (client_id, username) = match connection_credentials {
            Credentials::Provider(provider_settings) => (
                format!(
                    "{}/{}",
                    provider_settings.device_id().to_owned(),
                    provider_settings.module_id().to_owned()
                ),
                //TODO: handle properties that are sent by client in username (modelId, authchain)
                Some(format!(
                    "{}/{}/{}/?api-version={}",
                    provider_settings.iothub_hostname().to_owned(),
                    provider_settings.device_id().to_owned(),
                    provider_settings.module_id().to_owned(),
                    API_VERSION.to_owned()
                )),
            ),
            Credentials::PlainText(creds) => (
                creds.client_id().to_owned(),
                Some(creds.username().to_owned()),
            ),
            Credentials::Anonymous(client_id) => (client_id.into(), None),
        };

        let client_id = if clean_session { None } else { Some(client_id) };

        let client = Client::new(
            client_id.clone(),
            username.clone(),
            None,
            io_source.clone(),
            DEFAULT_MAX_RECONNECT,
            keep_alive,
        );

        let in_flight_permits = Arc::new(Semaphore::new(max_in_flight));
        let inner_publish_handle = ClientPublishHandle(client.publish_handle()?);
        let in_flight_handle = InFlightPublishHandle::new(inner_publish_handle, in_flight_permits);

        Ok(Self {
            client_id,
            username,
            io_source,
            keep_alive,
            client,
            event_handler,
            in_flight_handle,
        })
    }

    fn token_source(connection_credentials: &Credentials) -> Option<SasTokenSource> {
        match connection_credentials {
            Credentials::Provider(_) | Credentials::PlainText(_) => {
                Some(SasTokenSource::new(connection_credentials.clone()))
            }
            Credentials::Anonymous(_) => None,
        }
    }

    pub fn shutdown_handle(&self) -> Result<ClientShutdownHandle, ShutdownError> {
        self.client
            .shutdown_handle()
            .map_or(Err(ShutdownError::ClientDoesNotExist), |shutdown_handle| {
                Ok(ClientShutdownHandle(shutdown_handle))
            })
    }

    pub fn publish_handle(&self) -> InFlightPublishHandle<ClientPublishHandle> {
        self.in_flight_handle.clone()
    }

    pub async fn handle_events(&mut self) {
        debug!("polling bridge client");

        while let Some(event) = self.client.try_next().await.unwrap_or_else(|e| {
            // TODO: handle the error by recreating the connection
            error!(error=%e, "failed to poll events");
            None
        }) {
            debug!("handling event {:?}", event);
            if let Err(e) = self.event_handler.handle(&event).await {
                error!(err = %e, "error processing event {:?}", event);
            }
        }
    }

    pub async fn subscribe(&mut self, topics: &[String]) -> Result<(), ClientError> {
        info!("subscribing to topics");
        let subscriptions = topics.iter().map(|topic| proto::SubscribeTo {
            topic_filter: topic.to_string(),
            qos: DEFAULT_QOS,
        });

        for subscription in subscriptions {
            debug!("subscribing to topic {}", subscription.topic_filter);
            self.client
                .subscribe(subscription)
                .map_err(ClientError::Subscribe)?;
        }

        let mut subacks: HashSet<_> = topics.iter().collect();
        if subacks.is_empty() {
            info!("has no topics to subscribe to");
            return Ok(());
        }

        // TODO: Don't wait for subscription updates before starting the bridge.
        //       We should move this logic to the handle events.
        //
        //       This is fine for now when dealing with only the upstream edge device.
        //       But when remote brokers are introduced this will be an issue.
        while let Some(event) = self
            .client
            .try_next()
            .await
            .map_err(ClientError::PollClient)?
        {
            if let Event::SubscriptionUpdates(subscriptions) = event {
                for subscription in subscriptions {
                    match subscription {
                        SubscriptionUpdateEvent::Subscribe(sub) => {
                            subacks.remove(&sub.topic_filter);
                            debug!("successfully subscribed to topic {}", &sub.topic_filter);
                        }
                        SubscriptionUpdateEvent::RejectedByServer(topic_filter) => {
                            subacks.remove(&topic_filter);
                            error!("subscription rejected by server {}", topic_filter);
                        }
                        SubscriptionUpdateEvent::Unsubscribe(topic_filter) => {
                            warn!("unsubscribed to {}", topic_filter);
                        }
                    }
                }

                info!("stopped waiting for subscriptions");
                break;
            }
        }

        if subacks.is_empty() {
            info!("successfully subscribed to topics");
        } else {
            error!(
                "failed to receive expected subacks for topics: {:?}",
                subacks.iter().map(ToString::to_string).collect::<String>(),
            );
        }

        Ok(())
    }
}

#[async_trait]
pub trait EventHandler {
    type Error: Display;

    async fn handle(&mut self, event: &Event) -> Result<Handled, Self::Error>;
}

#[derive(Debug, PartialEq)]
pub enum Handled {
    Fully,
    Partially,
    Skipped,
}

#[derive(Debug, thiserror::Error)]
pub enum ClientError {
    #[error("failed to subscribe topic")]
    Subscribe(#[from] UpdateSubscriptionError),

    #[error("failed to poll client")]
    PollClient(#[from] mqtt3::Error),

    #[error("failed to shutdown custom mqtt client: {0}")]
    ShutdownClient(#[from] mqtt3::ShutdownError),

    #[error("failed to shutdown custom mqtt client: {0}")]
    PublishHandle(#[from] mqtt3::PublishError),

    #[error("failed to connect")]
    SslHandshake,
}

#[cfg(test)]
mod tests {
    use std::sync::atomic::{AtomicU8, Ordering};
    use std::{sync::Arc, time::Duration};

    use bytes::Bytes;
    use futures_util::{future::join3, stream::FuturesUnordered};
    use mqtt3::proto::{Publication, QoS};
    use tokio::{
        stream::StreamExt,
        sync::{
            mpsc::{self},
            Mutex, Semaphore,
        },
        time,
    };

    use super::InFlightPublishHandle;
    use super::MockPublishHandle;

    // Create InFlightPublishHandle with mock publish handle and capacity 2 in-flight
    // Mock publish handle has internal counter for pubs
    // It also blocks on signal that signals messages can be sent
    //
    // Get two publish futures from handle (i.e. max in-flight)
    // Start 3 tasks
    // 1 - poll the in-flight publish futures
    // 2 - get another publish futures and await it
    // 3 - test verification logic that will wait to make sure all messages are trying to be sent, but blocked
    //     asserts max messages in flight
    //     signals that messages can send
    //     asserts all three messages have been in flight
    // Join all these three tasks to make sure all messages were sent
    #[tokio::test]
    async fn limits_in_flight_pubs() {
        let counter = Arc::new(AtomicU8::new(0));
        let (mut sender, receiver) = mpsc::channel::<()>(1);
        let receiver = Arc::new(Mutex::new(receiver));
        let publish_handle = MockPublishHandle::new(counter.clone(), receiver);

        let permits = Arc::new(Semaphore::new(2));
        let publish_handle = InFlightPublishHandle::new(publish_handle, permits);

        let mut futures_unordered = FuturesUnordered::new();
        let publication = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let pub_fut1 = publish_handle.publish_future(publication.clone()).await;
        let pub_fut2 = publish_handle.publish_future(publication.clone()).await;
        futures_unordered.push(pub_fut1);
        futures_unordered.push(pub_fut2);

        let verification = async move {
            // make sure two messages in flight even though three are sending
            time::delay_for(Duration::from_secs(2)).await;
            assert_eq!(counter.load(Ordering::Relaxed), 2);

            // signal that messages can all send
            sender.send(()).await.unwrap();
            time::delay_for(Duration::from_secs(2)).await;

            // make sure last messages makes it in flight
            assert_eq!(counter.load(Ordering::Relaxed), 3);
            println!("all messages in flight");
        };

        let in_flight = async move {
            for _ in 0..2 as u8 {
                println!("polling publishes");
                futures_unordered.next().await;
            }
        };

        let next_publish = async move {
            let pub_fut2 = publish_handle.publish_future(publication.clone()).await;
            pub_fut2.await;
        };

        join3(verification, in_flight, next_publish).await;
    }
}
