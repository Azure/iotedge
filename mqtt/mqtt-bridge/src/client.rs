#![allow(dead_code)] // TODO remove when ready
use std::{
    collections::HashSet, fmt::Display, io::Error, io::ErrorKind, pin::Pin, str, time::Duration,
};

use chrono::Utc;
use futures_util::future::{self, BoxFuture};
use openssl::{ssl::SslConnector, ssl::SslMethod, x509::X509};
use tokio::{io::AsyncRead, io::AsyncWrite, net::TcpStream, stream::StreamExt};
use tracing::{debug, error, warn};

use mqtt3::{
    proto, Client, Event, IoSource, PublishHandle, ShutdownError, SubscriptionUpdateEvent,
    UpdateSubscriptionError,
};

use crate::{
    settings::Credentials,
    token_source::TrustBundleSource,
    token_source::{SasTokenSource, TokenSource},
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

/// This is a wrapper over mqtt3 client
pub struct MqttClient<T>
where
    T: EventHandler,
{
    client_id: Option<String>,
    username: Option<String>,
    io_source: BridgeIoSource,
    keep_alive: Duration,
    client: Client<BridgeIoSource>,
    event_handler: T,
}

impl<T: EventHandler> MqttClient<T> {
    pub fn tcp(
        address: &str,
        keep_alive: Duration,
        clean_session: bool,
        event_handler: T,
        connection_credentials: &Credentials,
    ) -> Self {
        let token_source = Self::token_source(&connection_credentials);
        let tcp_connection =
            TcpConnection::<SasTokenSource>::new(address.to_owned(), token_source, None);
        let io_source = BridgeIoSource::Tcp(tcp_connection);

        Self::new(
            keep_alive,
            clean_session,
            event_handler,
            connection_credentials,
            io_source,
        )
    }

    pub fn tls(
        address: &str,
        keep_alive: Duration,
        clean_session: bool,
        event_handler: T,
        connection_credentials: &Credentials,
    ) -> Self {
        let trust_bundle = Some(TrustBundleSource::new(connection_credentials.clone()));

        let token_source = Self::token_source(&connection_credentials);
        let tcp_connection =
            TcpConnection::<SasTokenSource>::new(address.to_owned(), token_source, trust_bundle);
        let io_source = BridgeIoSource::Tls(tcp_connection);

        Self::new(
            keep_alive,
            clean_session,
            event_handler,
            connection_credentials,
            io_source,
        )
    }

    fn new(
        keep_alive: Duration,
        clean_session: bool,
        event_handler: T,
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

    pub fn shutdown_handle(&self) -> Result<ClientShutdownHandle, ShutdownError> {
        self.client
            .shutdown_handle()
            .map_or(Err(ShutdownError::ClientDoesNotExist), |shutdown_handle| {
                Ok(ClientShutdownHandle(shutdown_handle))
            })
    }

    pub fn publish_handle(&self) -> Result<PublishHandle, ClientError> {
        let publish_handle = self
            .client
            .publish_handle()
            .map_err(ClientError::PublishHandle)?;

        Ok(publish_handle)
    }

    pub async fn handle_events(&mut self) {
        // TODO REVIEW: Is client id ever not set
        let client_id = self.client_id.clone().unwrap_or(String::new());
        debug!("polling bridge client {}", client_id);

        while let Some(event) = self.client.try_next().await.unwrap_or_else(|e| {
            error!(message = "failed to poll events", error=%e);
            // TODO: handle the error by recreting the connection
            None
        }) {
            debug!("handle event {:?}", event);
            if let Err(e) = self.event_handler.handle_event(event) {
                error!("error processing event {}", e);
            }
        }
    }

    pub async fn subscribe(&mut self, topics: &Vec<String>) -> Result<(), ClientError> {
        debug!("subscribing to topics");
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
            debug!("no topics to subscribe to");
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
                            debug!("successfully subscribed to topics");
                        }
                        SubscriptionUpdateEvent::RejectedByServer(topic_filter) => {
                            subacks.remove(&topic_filter);
                            error!("subscription rejected by server {}", topic_filter);
                        }
                        SubscriptionUpdateEvent::Unsubscribe(topic_filter) => {
                            warn!("Unsubscribed {}", topic_filter);
                        }
                    }
                }

                debug!("stop waiting for subscriptions");
                break;
            }
        }

        if subacks.is_empty() {
            debug!("successfully subscribed to topics");
        } else {
            error!(
                "failed to receive expected subacks for topics: {0:?}",
                subacks.iter().map(ToString::to_string).collect::<String>()
            );
        }

        Ok(())
    }
}

pub trait EventHandler {
    type Error: Display;

    fn handle_event(&mut self, event: Event) -> Result<(), Self::Error>;
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
