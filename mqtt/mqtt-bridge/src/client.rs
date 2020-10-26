#![allow(clippy::default_trait_access)] // Needed because mock! macro violates
#![allow(dead_code)] // TODO remove when ready
use std::{fmt::Display, io::Error, io::ErrorKind, pin::Pin, str, time::Duration};

use async_trait::async_trait;
use chrono::Utc;
use futures_util::future::{self, BoxFuture};
use mockall::automock;
use openssl::{ssl::SslConnector, ssl::SslMethod, x509::X509};
use tokio::{io::AsyncRead, io::AsyncWrite, net::TcpStream, stream::StreamExt};
use tracing::{debug, error, info};

use mqtt3::{
    proto::{self, Publication, SubscribeTo},
    Client, Event, IoSource, PublishError, ShutdownError, UpdateSubscriptionError,
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

pub struct MqttClientConfig {
    addr: String,
    keep_alive: Duration,
    clean_session: bool,
    credentials: Credentials,
}

impl MqttClientConfig {
    pub fn new(
        addr: impl Into<String>,
        keep_alive: Duration,
        clean_session: bool,
        credentials: Credentials,
    ) -> Self {
        Self {
            addr: addr.into(),
            keep_alive,
            clean_session,
            credentials,
        }
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
}

impl<H> MqttClient<H>
where
    H: MqttEventHandler,
{
    pub fn tcp(config: MqttClientConfig, event_handler: H) -> Self {
        let token_source = Self::token_source(&config.credentials);
        let tcp_connection = TcpConnection::new(config.addr, token_source, None);
        let io_source = BridgeIoSource::Tcp(tcp_connection);

        Self::new(
            config.keep_alive,
            config.clean_session,
            event_handler,
            &config.credentials,
            io_source,
        )
    }

    pub fn tls(config: MqttClientConfig, event_handler: H) -> Self {
        let trust_bundle = Some(TrustBundleSource::new(config.credentials.clone()));

        let token_source = Self::token_source(&config.credentials);
        let tcp_connection = TcpConnection::new(config.addr, token_source, trust_bundle);
        let io_source = BridgeIoSource::Tls(tcp_connection);

        Self::new(
            config.keep_alive,
            config.clean_session,
            event_handler,
            &config.credentials,
            io_source,
        )
    }

    fn new(
        keep_alive: Duration,
        clean_session: bool,
        event_handler: H,
        connection_credentials: &Credentials,
        io_source: BridgeIoSource,
    ) -> Self {
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

        Self {
            client_id,
            username,
            io_source,
            keep_alive,
            client,
            event_handler,
        }
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
            if let Err(e) = self.event_handler.handle(event).await {
                error!(err = %e, "error processing event");
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

        Ok(())
    }
}

/// A trait extending `MqttClient` with additional handles functionality.
pub trait MqttClientExt {
    /// Publish handle type.
    type PublishHandle;

    /// Returns an instance of publish handle.
    fn publish_handle(&self) -> Result<Self::PublishHandle, ClientError>;

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
    type PublishHandle = PublishHandle;

    fn publish_handle(&self) -> Result<Self::PublishHandle, ClientError> {
        let publish_handle = self.client.publish_handle()?;
        Ok(PublishHandle(publish_handle))
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
    type PublishHandle = MockPublishHandle;

    fn publish_handle(&self) -> Result<Self::PublishHandle, ClientError> {
        Ok(MockPublishHandle::new())
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

/// A client publish handle.
#[derive(Debug, Clone)]
pub struct PublishHandle(mqtt3::PublishHandle);

impl PublishHandle {
    pub async fn publish(&mut self, publication: Publication) -> Result<(), PublishError> {
        self.0.publish(publication).await
    }
}

mockall::mock! {
    pub PublishHandle {
        async fn publish(&mut self, publication: Publication) -> Result<(), PublishError>;
    }

    pub trait Clone {
        fn clone(&self) -> Self;
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
pub trait MqttEventHandler {
    type Error: Display;

    /// Handles MQTT client event and returns marker which determines whether
    /// an event handler fully handled an event.
    async fn handle(&mut self, event: Event) -> Result<Handled, Self::Error>;
}

/// An `MqttEventHandler::handle` method result.
#[derive(Debug, PartialEq)]
pub enum Handled {
    /// MQTT client event is fully handled.
    Fully,

    /// MQTT client event is partially handled. It contains modified event.
    Partially(Event),

    /// Unknown MQTT client event so event handler skipped the event.
    /// It contains not modified event.
    Skipped(Event),
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
