#![allow(dead_code)] // TODO remove when ready
use std::{collections::HashSet, fmt::Display, pin::Pin, str, time::Duration};

use async_trait::async_trait;
use chrono::Utc;
use futures_util::future::BoxFuture;
use native_tls::{Certificate, TlsConnector};
use openssl::x509::X509;
use tokio::{io::AsyncRead, io::AsyncWrite, net::TcpStream, stream::StreamExt};
use tracing::{debug, error};

use mqtt3::{
    proto, Client, Event, IoSource, ShutdownError, SubscriptionUpdateEvent, UpdateSubscriptionError,
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

#[derive(Debug)]
pub struct ShutdownHandle(mqtt3::ShutdownHandle);

impl ShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), ClientConnectError> {
        self.0
            .shutdown()
            .await
            .map_err(ClientConnectError::ShutdownClient)?;
        Ok(())
    }
}

enum BridgeIoSource {
    Tcp(TcpConnection<SasTokenSource>),
    Tls(TcpConnection<SasTokenSource>),
}

trait BridgeIo: AsyncRead + AsyncWrite + Send + Sync + 'static {}

impl<I> BridgeIo for I where I: AsyncRead + AsyncWrite + Send + Sync + 'static {}

pub struct TcpConnection<T>
where
    T: TokenSource + Clone + Send + Sync + 'static,
{
    address: String,
    port: Option<String>,
    token_source: Option<T>,
    trust_bundle_source: Option<TrustBundleSource>,
}

impl<T> TcpConnection<T>
where
    T: TokenSource + Clone + Send + Sync + 'static,
{
    pub fn new(
        address: String,
        port: Option<String>,
        token_source: Option<T>,
        trust_bundle_source: Option<TrustBundleSource>,
    ) -> Self {
        Self {
            address,
            port,
            token_source,
            trust_bundle_source,
        }
    }
}

impl IoSource for BridgeIoSource {
    type Io = Pin<Box<dyn BridgeIo>>;
    type Error = std::io::Error;
    type Future = BoxFuture<'static, Result<(Self::Io, Option<String>), std::io::Error>>;

    fn connect(&mut self) -> Self::Future {
        match self {
            BridgeIoSource::Tcp(connect) => {
                let address = connect.address.clone();
                let port = connect.port.clone();
                let token_source = connect.token_source.as_ref().cloned();

                Box::pin(async move {
                    let expiry =
                        Utc::now() + chrono::Duration::minutes(DEFAULT_TOKEN_DURATION_MINS);

                    let password: Option<String> = if let Some(ts) = token_source {
                        ts.get(&expiry)
                            .await
                            .map_err(|e| {
                                error!("Failed to get token for connection {} {}", address, e);
                                e
                            })
                            .ok()
                    } else {
                        None
                    };

                    let host =
                        port.map_or(address.clone(), |p| format!("{}:{}", address.clone(), p));

                    let io = TcpStream::connect(&host).await;
                    debug!("Tcp connection {:?} for {:?}", io, address);
                    io.map(|io| {
                        let stream: Pin<Box<dyn BridgeIo>> = Box::pin(io);
                        (stream, password)
                    })
                })
            }
            BridgeIoSource::Tls(connect) => {
                let address = connect.address.clone();
                let port = connect.port.clone();
                let token_source = connect.token_source.as_ref().cloned();
                let trust_bundle_source = connect.trust_bundle_source.clone();

                Box::pin(async move {
                    let expiry =
                        Utc::now() + chrono::Duration::minutes(DEFAULT_TOKEN_DURATION_MINS);

                    let host =
                        port.map_or(address.clone(), |p| format!("{}:{}", address.clone(), p));
                    let stream = TcpStream::connect(host)
                        .await
                        .map_err(|err| std::io::Error::new(std::io::ErrorKind::Other, err))?;

                    let server_root_certificate = if let Some(source) = trust_bundle_source {
                        source.get_trust_bundle().await.map_or_else(
                            |e| {
                                error!(
                                    "Failed to get trust bundle for connection {} {}",
                                    address, e
                                );
                                None
                            },
                            |cert| cert,
                        )
                    } else {
                        None
                    };

                    debug!("Trust bundle {:?}", server_root_certificate);

                    let password: Option<String> = if let Some(ts) = token_source {
                        ts.get(&expiry)
                            .await
                            .map_err(|e| {
                                error!("Failed to get token for connection {} {}", address, e);
                                e
                            })
                            .ok()
                    } else {
                        None
                    };

                    let mut builder = TlsConnector::builder();

                    if let Some(trust_bundle) = server_root_certificate {
                        let certs = X509::stack_from_pem(trust_bundle.as_bytes())
                            .unwrap()
                            .into_iter()
                            .map(|cert| Certificate::from_der(&cert.to_der().unwrap()).unwrap());
                        debug!("{}", certs.len());
                        for cert in certs {
                            debug!("found cert");
                            builder.add_root_certificate(cert);
                        }
                    }

                    let connector = builder.build().map_err(|err| {
                        std::io::Error::new(
                            std::io::ErrorKind::Other,
                            format!("could not create TLS connector: {}", err),
                        )
                    })?;
                    let connector: tokio_tls::TlsConnector = connector.into();

                    let io = connector.connect(&address, stream).await;

                    debug!("Tls connection {:?} for {:?}", io, address);

                    io.map_or_else(
                        |e| Err(std::io::Error::new(std::io::ErrorKind::Other, e)),
                        |io| {
                            let stream: Pin<Box<dyn BridgeIo>> = Box::pin(io);
                            Ok((stream, password))
                        },
                    )
                })
            }
        }
    }
}

pub struct MqttClient<T>
where
    T: EventHandler,
{
    client: Client<BridgeIoSource>,
    event_handler: T,
}

impl<T: EventHandler> MqttClient<T> {
    pub fn new(
        address: &str,
        port: Option<String>,
        keep_alive: Duration,
        _clean_session: bool,
        event_handler: T,
        connection_credentials: &Credentials,
        secure: bool,
    ) -> Self {
        let (client_id, username, token_source) = match connection_credentials {
            Credentials::Provider(provider_settings) => (
                provider_settings.device_id().into(),
                //TODO: handle properties that are sent by client in username (modelId, authchain)
                Some(format!(
                    "{}/{}/{}/?api-version=2010-01-01",
                    provider_settings.iothub_hostname().to_owned(),
                    provider_settings.device_id().to_owned(),
                    provider_settings.module_id().to_owned()
                )),
                Some(SasTokenSource::new(connection_credentials.clone())),
            ),
            Credentials::PlainText(creds) => (
                creds.client_id().to_owned(),
                Some(creds.username().to_owned()),
                Some(SasTokenSource::new(connection_credentials.clone())),
            ),
            Credentials::Anonymous(client_id) => (client_id.into(), None, None),
        };

        let client = if !secure {
            Client::new(
                Some(client_id),
                username,
                None,
                BridgeIoSource::Tcp(TcpConnection::<SasTokenSource>::new(
                    address.to_owned(),
                    port,
                    token_source,
                    None,
                )),
                DEFAULT_MAX_RECONNECT,
                keep_alive,
            )
        } else {
            Client::new(
                Some(client_id),
                username,
                None,
                BridgeIoSource::Tls(TcpConnection::<SasTokenSource>::new(
                    address.to_owned(),
                    port,
                    token_source,
                    Some(TrustBundleSource::new(connection_credentials.clone())),
                )),
                DEFAULT_MAX_RECONNECT,
                keep_alive,
            )
        };

        Self {
            client,
            event_handler,
        }
    }

    pub fn shutdown_handle(&self) -> Result<ShutdownHandle, ShutdownError> {
        self.client
            .shutdown_handle()
            .map_or(Err(ShutdownError::ClientDoesNotExist), |shutdown_handle| {
                Ok(ShutdownHandle(shutdown_handle))
            })
    }

    pub async fn handle_events(mut self) -> Result<(), ClientConnectError> {
        while let Some(event) = self.client.try_next().await.unwrap_or_else(|e| {
            error!(message = "failed to poll events", error=%e);
            None
        }) {
            debug!("handle event {:?}", event);
            if let Err(e) = self.event_handler.handle_event(event).await {
                error!("error processing event {}", e);
            }
        }

        Ok(())
    }

    pub async fn subscribe(&mut self, topics: Vec<String>) -> Result<(), ClientConnectError> {
        debug!("subscribing to topics");
        let subscriptions = topics.iter().map(|topic| proto::SubscribeTo {
            topic_filter: topic.to_string(),
            qos: DEFAULT_QOS,
        });

        for subscription in subscriptions {
            debug!("subscribing to topic {}", subscription.topic_filter);
            self.client
                .subscribe(subscription)
                .map_err(ClientConnectError::SubscribeFailure)?;
        }

        let mut subacks: HashSet<_> = topics.iter().collect();
        if subacks.is_empty() {
            debug!("no topics to subscribe to");
            return Ok(());
        }

        while let Some(event) = self
            .client
            .try_next()
            .await
            .map_err(ClientConnectError::PollClientFailure)?
        {
            if let Event::SubscriptionUpdates(subscriptions) = event {
                for subscription in subscriptions {
                    match subscription {
                        SubscriptionUpdateEvent::Subscribe(sub) => {
                            subacks.remove(&sub.topic_filter);
                        }
                        SubscriptionUpdateEvent::SubscriptionRejectedByServer(sub) => {
                            subacks.remove(&sub);
                            error!("SubscriptionRejectedByServer {}", sub);
                        }
                        _ => {}
                    }
                }
            }
        }

        if subacks.is_empty() {
            debug!("command handler successfully subscribed to disconnect topic");
        } else {
            error!(
                "failed to receive expected subacks for topics: {0:?}",
                subacks
                    .iter()
                    .map(std::string::ToString::to_string)
                    .collect::<String>()
            );
        }

        Ok(())
    }
}

#[async_trait]
pub trait EventHandler {
    type Error: Display;

    async fn handle_event(&mut self, event: Event) -> Result<(), Self::Error>;
}

#[derive(Debug, thiserror::Error)]
pub enum ClientConnectError {
    #[error("failed to subscribe topic")]
    SubscribeFailure(#[from] UpdateSubscriptionError),

    #[error("failed to poll client")]
    PollClientFailure(#[from] mqtt3::Error),

    #[error("failed to shutdown custom mqtt client: {0}")]
    ShutdownClient(#[from] mqtt3::ShutdownError),
}
