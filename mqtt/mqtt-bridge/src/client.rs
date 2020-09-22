#![allow(dead_code)] // TODO remove when ready
use std::{collections::HashSet, fmt::Display, time::Duration};

use async_trait::async_trait;
use chrono::Utc;
use futures_util::future::BoxFuture;
use tokio::{net::TcpStream, stream::StreamExt};
use tracing::{debug, error};

use mqtt3::{proto, Client, Event, IoSource, ShutdownError, UpdateSubscriptionError};

use crate::{
    settings::Credentials,
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

pub struct TcpConnection<T>
where
    T: TokenSource + Clone + Send + Sync + 'static,
{
    address: String,
    token_source: Option<T>,
}

impl<T> TcpConnection<T>
where
    T: TokenSource + Clone + Send + Sync + 'static,
{
    pub fn new(address: String, token_source: Option<T>) -> Self {
        Self {
            address,
            token_source,
        }
    }
}

impl<T> IoSource for TcpConnection<T>
where
    T: TokenSource + Clone + Send + Sync + 'static,
{
    type Io = TcpStream;
    type Error = std::io::Error;
    type Future = BoxFuture<'static, Result<(TcpStream, Option<String>), std::io::Error>>;

    fn connect(&mut self) -> Self::Future {
        let address = self.address.clone();
        let token_source = self.token_source.as_ref().cloned();

        Box::pin(async move {
            let expiry = Utc::now() + chrono::Duration::minutes(DEFAULT_TOKEN_DURATION_MINS);

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

            let io = TcpStream::connect(address).await;
            io.map(|io| (io, password))
        })
    }
}

pub struct MqttClient<T>
where
    T: EventHandler,
{
    client: Client<TcpConnection<SasTokenSource>>,
    event_handler: T,
}

impl<T: EventHandler> MqttClient<T> {
    pub fn new(
        address: &str,
        keep_alive: Duration,
        clean_session: bool,
        event_handler: T,
        connection_credentials: &Credentials,
    ) -> Self {
        let (client_id, username, token_source) = match connection_credentials {
            Credentials::Provider(provider_settings) => (
                provider_settings.device_id().into(),
                //TODO: handle properties that are sent by client in username (modelId, authchain)
                Some(format!(
                    "{}/{}/{}",
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

        let client = if clean_session {
            Client::new(
                Some(client_id),
                username,
                None,
                TcpConnection::new(address.into(), token_source),
                DEFAULT_MAX_RECONNECT,
                keep_alive,
            )
        } else {
            Client::from_state(
                client_id,
                username,
                None,
                TcpConnection::new(address.into(), token_source),
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

        let subacks: HashSet<_> = topics.iter().collect();
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
            //TODO: change the mqtt client to send an error back when the subscriotion fails instead of reconnecting and resending the sub
            //right now it can't detect if the the broker doesn't allow to subscribe to the specific topic, but it will send a connect event
            if let Event::NewConnection { reset_session: _ } = event {
                return Ok(());
            }
        }

        error!("failed to subscribe to topics");
        Err(ClientConnectError::MissingSubacks(
            subacks
                .into_iter()
                .map(std::string::ToString::to_string)
                .collect(),
        ))
    }
}

#[async_trait]
pub trait EventHandler {
    type Error: Display;

    async fn handle_event(&mut self, event: Event) -> Result<(), Self::Error>;
}

#[derive(Debug, thiserror::Error)]
pub enum ClientConnectError {
    #[error("failed to receive expected subacks for topics: {0:?}")]
    MissingSubacks(Vec<String>),

    #[error("failed to subscribe topic")]
    SubscribeFailure(#[from] UpdateSubscriptionError),

    #[error("failed to poll client")]
    PollClientFailure(#[from] mqtt3::Error),

    #[error("failed to shutdown custom mqtt client: {0}")]
    ShutdownClient(#[from] mqtt3::ShutdownError),
}
