#![allow(dead_code)] // TODO remove when ready
use std::{
    collections::HashSet,
    fmt::Display,
    io::{Error, ErrorKind},
    pin::Pin,
    str,
    sync::{
        atomic::{AtomicU8, Ordering},
        Arc,
    },
    time::Duration,
};

use async_trait::async_trait;
use chrono::Utc;
use futures_util::future::{self, BoxFuture};
use mockall::{automock, mock};
use openssl::{ssl::SslConnector, ssl::SslMethod, x509::X509};
use tokio::{
    io::{AsyncRead, AsyncWrite},
    net::TcpStream,
    stream::StreamExt,
    sync::{mpsc::Receiver, Mutex, Semaphore},
};
use tracing::{debug, error, info, warn};

use mqtt3::{
    proto::{self, Publication, SubscribeTo},
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
        address: impl Into<String>,
        token_source: Option<T>,
        trust_bundle_source: Option<TrustBundleSource>,
    ) -> Self {
        Self {
            address: address.into(),
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

/// This is a wrapper over mqtt3 client
pub struct MqttClient<H> {
    client_id: Option<String>,
    username: Option<String>,
    io_source: BridgeIoSource,
    keep_alive: Duration,
    client: Client<BridgeIoSource>,
    event_handler: H,
    in_flight_handle: InFlightPublishHandle<PublishHandle>,
}

impl<H: EventHandler> MqttClient<H> {
    pub fn tcp(config: MqttClientConfig, event_handler: H) -> Result<Self, ClientError> {
        let token_source = Self::token_source(&config.credentials);
        let tcp_connection = TcpConnection::new(config.addr, token_source, None);
        let io_source = BridgeIoSource::Tcp(tcp_connection);
        let max_in_flight = config.max_in_flight;

        Self::new(
            config.keep_alive,
            config.clean_session,
            event_handler,
            &config.credentials,
            io_source,
            max_in_flight,
        )
    }

    pub fn tls(config: MqttClientConfig, event_handler: H) -> Result<Self, ClientError> {
        let trust_bundle = Some(TrustBundleSource::new(config.credentials.clone()));

        let token_source = Self::token_source(&config.credentials);
        let tcp_connection = TcpConnection::new(config.addr, token_source, trust_bundle);
        let io_source = BridgeIoSource::Tls(tcp_connection);
        let max_in_flight = config.max_in_flight;

        Self::new(
            config.keep_alive,
            config.clean_session,
            event_handler,
            &config.credentials,
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

        let inner_publish_handle = PublishHandle(client.publish_handle()?);
        let in_flight_handle = InFlightPublishHandle::new(inner_publish_handle, max_in_flight);

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

/// A trait extending `MqttClient` with additional handles functionality.
pub trait MqttClientExt {
    /// Publish handle type.
    type PublishHandle;

    /// Returns an instance of publish handle.
    fn publish_handle(&self) -> Self::PublishHandle;

    /// Update subscription handle type.
    type UpdateSubscriptionHandle;

    /// Returns an instance of update subscription handle.
    fn update_subscription_handle(&self) -> Result<Self::UpdateSubscriptionHandle, ClientError>;

    /// Client shutdown handle type.
    type ShutdownHandle;

    /// Returns an instance of shutdown handle.
    fn shutdown_handle(&self) -> Result<Self::ShutdownHandle, ShutdownError>;
}

#[cfg(not(test))]
/// Implements `MqttClientExt` for production code.
impl<H> MqttClientExt for MqttClient<H> {
    type PublishHandle = InFlightPublishHandle<PublishHandle>;

    fn publish_handle(&self) -> Self::PublishHandle {
        self.in_flight_handle.clone()
    }

    type UpdateSubscriptionHandle = UpdateSubscriptionHandle;

    fn update_subscription_handle(&self) -> Result<Self::UpdateSubscriptionHandle, ClientError> {
        let update_subscription_handle = self.client.update_subscription_handle()?;
        Ok(UpdateSubscriptionHandle(update_subscription_handle))
    }

    type ShutdownHandle = ShutdownHandle;

    fn shutdown_handle(&self) -> Result<Self::ShutdownHandle, ShutdownError> {
        let shutdown_handle = self.client.shutdown_handle()?;
        Ok(ShutdownHandle(shutdown_handle))
    }
}

#[cfg(test)]
/// Implements `MqttClientExt` for tests.
impl<H> MqttClientExt for MqttClient<H> {
    type PublishHandle = InFlightPublishHandle<MockallPublishHandleWrapper>;

    fn publish_handle(&self) -> Self::PublishHandle {
        InFlightPublishHandle::new(MockallPublishHandleWrapper::new(), 100)
    }

    type UpdateSubscriptionHandle = MockUpdateSubscriptionHandle;

    fn update_subscription_handle(&self) -> Result<Self::UpdateSubscriptionHandle, ClientError> {
        Ok(MockUpdateSubscriptionHandle::new())
    }

    type ShutdownHandle = MockShutdownHandle;

    fn shutdown_handle(&self) -> Result<Self::ShutdownHandle, ShutdownError> {
        Ok(MockShutdownHandle::new())
    }
}

/// A client shutdown handle.
#[derive(Debug, Clone)]
pub struct ShutdownHandle(mqtt3::ShutdownHandle);

#[automock]
impl ShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), ClientError> {
        self.0.shutdown().await?;
        Ok(())
    }
}

pub struct MqttClientConfig {
    addr: String,
    keep_alive: Duration,
    clean_session: bool,
    credentials: Credentials,
    max_in_flight: usize,
}

impl MqttClientConfig {
    pub fn new(
        addr: impl Into<String>,
        keep_alive: Duration,
        clean_session: bool,
        credentials: Credentials,
        max_in_flight: usize,
    ) -> Self {
        Self {
            addr: addr.into(),
            keep_alive,
            clean_session,
            credentials,
            max_in_flight,
        }
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
    pub fn new(publish_handle: P, max_in_flight: usize) -> Self {
        let permits = Arc::new(Semaphore::new(max_in_flight));

        Self {
            publish_handle,
            permits,
        }
    }

    pub async fn publish_future(
        &self,
        publication: Publication,
    ) -> BoxFuture<'static, Result<(), PublishError>> {
        let permits = self.permits.clone();
        let permit = permits.acquire_owned().await;
        let mut publish_handle = self.publish_handle.clone();

        let publication_send = async move {
            publish_handle.publish(publication).await?;
            drop(permit);
            Ok(())
        };

        Box::pin(publication_send)
    }
}

/// Trait used to facilitate mock publish handle types
#[async_trait]
pub trait InnerPublishHandle {
    async fn publish(&mut self, publication: Publication) -> Result<(), PublishError>;
}

/// A client publish handle.
#[derive(Debug, Clone)]
pub struct PublishHandle(mqtt3::PublishHandle);

#[async_trait]
impl InnerPublishHandle for PublishHandle {
    async fn publish(&mut self, publication: Publication) -> Result<(), PublishError> {
        self.0.publish(publication).await
    }
}

/// A wrapper around mockall publish handle necessary for cloning
#[derive(Clone)]
pub struct MockallPublishHandleWrapper {
    inner: Arc<Mutex<MockPublishHandle>>,
}

impl MockallPublishHandleWrapper {
    pub fn new() -> Self {
        let inner = Arc::new(Mutex::new(MockPublishHandle::new()));
        Self { inner }
    }

    pub fn inner(&self) -> Arc<Mutex<MockPublishHandle>> {
        self.inner.clone()
    }
}

#[async_trait]
impl InnerPublishHandle for MockallPublishHandleWrapper {
    async fn publish(&mut self, publication: Publication) -> Result<(), PublishError> {
        self.inner.lock().await.publish(publication).await
    }
}

mock! {
    pub PublishHandle {}

    #[async_trait]
    pub trait InnerPublishHandle {
        async fn publish(&mut self, publication: Publication) -> Result<(), PublishError>;
    }
}

/// Mock publish handle mimicking the mqtt3 publish handle.
/// Used for unit tests only.
/// This publish handle needs to implement clone which necessitates Arc and Mutex.
#[derive(Debug, Clone)]
pub struct CountingMockPublishHandle {
    counter: Arc<AtomicU8>,
    send_trigger: Arc<Mutex<Receiver<()>>>,
}

impl CountingMockPublishHandle {
    fn new(counter: Arc<AtomicU8>, send_trigger: Arc<Mutex<Receiver<()>>>) -> Self {
        Self {
            counter,
            send_trigger,
        }
    }
}

#[async_trait]
impl InnerPublishHandle for CountingMockPublishHandle {
    async fn publish(&mut self, _: Publication) -> Result<(), PublishError> {
        self.counter.fetch_add(1, Ordering::Relaxed);
        self.send_trigger.lock().await.next().await;
        Ok(())
    }
}

/// A client subscription update handle.
pub struct UpdateSubscriptionHandle(mqtt3::UpdateSubscriptionHandle);

#[automock]
impl UpdateSubscriptionHandle {
    pub async fn subscribe(
        &mut self,
        subscribe_to: SubscribeTo,
    ) -> Result<(), UpdateSubscriptionError> {
        self.0.subscribe(subscribe_to).await
    }

    pub async fn unsubscribe(
        &mut self,
        unsubscribe_from: String,
    ) -> Result<(), UpdateSubscriptionError> {
        self.0.unsubscribe(unsubscribe_from).await
    }
}

/// A trait which every MQTT client event handler implements.
#[async_trait]
pub trait EventHandler {
    type Error: Display;

    /// Handles MQTT client event and returns marker which determines whether
    /// an event handler fully handled an event.
    async fn handle(&mut self, event: &Event) -> Result<Handled, Self::Error>;
}

/// An `EventHandler::handle` method result.
#[derive(Debug, PartialEq)]
pub enum Handled {
    /// MQTT client event is fully handled.
    Fully,

    /// MQTT client event is partially handled.
    Partially,

    /// Unknown MQTT client event so event handler skipped the event.
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

    #[error("failed to obtain publish handle: {0}")]
    PublishHandle(#[from] mqtt3::PublishError),

    #[error("failed to obtain subscribe handle: {0}")]
    UpdateSubscriptionHandle(#[source] mqtt3::UpdateSubscriptionError),

    #[error("failed to connect")]
    SslHandshake,
}

#[cfg(test)]
mod tests {
    use std::{
        sync::{
            atomic::{AtomicU8, Ordering},
            Arc,
        },
        time::Duration,
    };

    use bytes::Bytes;
    use futures_util::{future::join3, stream::FuturesUnordered};
    use mqtt3::proto::{Publication, QoS};
    use tokio::{
        stream::StreamExt,
        sync::{
            mpsc::{self},
            Mutex,
        },
        time,
    };

    use super::{CountingMockPublishHandle, InFlightPublishHandle};

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
        let publish_handle = CountingMockPublishHandle::new(counter.clone(), receiver);

        let publish_handle = InFlightPublishHandle::new(publish_handle, 2);

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
        };

        let in_flight = async move {
            for _ in 0..2 as u8 {
                futures_unordered.next().await;
            }
        };

        let next_publish = async move {
            let pub_fut2 = publish_handle.publish_future(publication.clone()).await;
            pub_fut2.await.unwrap();
        };

        join3(verification, in_flight, next_publish).await;
    }
}
