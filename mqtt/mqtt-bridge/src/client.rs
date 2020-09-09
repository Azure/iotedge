#![allow(dead_code)] // TODO remove when ready
use std::{collections::HashSet, time::Duration};

use async_trait::async_trait;
use chrono::Utc;
use futures_util::future::BoxFuture;
use tokio::{net::TcpStream, stream::StreamExt};
use tracing::{debug, error};

use mqtt3::{
    proto, Client, Event, IoSource, ShutdownError, SubscriptionUpdateEvent, UpdateSubscriptionError,
};

use crate::settings::Credentials;
use crate::token_source::{TokenSource, SasTokenSource};

const DEFAULT_TOKEN_DURATION: Duration = Duration::from_secs(60 * 60);

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
            let expiry = Utc::now() + chrono::Duration::from_std(DEFAULT_TOKEN_DURATION).unwrap();
            let mut password = None;
            if let Some(ts) = token_source {
                // TODO: handle error
                password = match ts.get(&expiry).await {
                    Ok(x) => Some(x),
                    Err(_) => {
                        //error!("Failed to get token {}", e);
                        None
                    },
                }
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
        let (client_id, token_source) = match connection_credentials {
            Credentials::Provider(provider_settings) => (
                provider_settings.device_id().into(),
                Some(SasTokenSource::new(connection_credentials.clone())),
            ),
            Credentials::PlainText(creds) => (
                creds.client_id().into(),
                Some(SasTokenSource::new(connection_credentials.clone())),
            ),
            Credentials::Anonymous(client_id) => (client_id.into(), None),
        };

        let client = if clean_session {
            Client::new(
                Some(client_id),
                None,
                None,
                TcpConnection {
                    address: address.into(),
                    token_source,
                },
                Duration::from_secs(1),
                keep_alive,
            )
        } else {
            Client::from_state(
                client_id,
                None,
                None,
                TcpConnection {
                    address: address.into(),
                    token_source,
                },
                Duration::from_secs(1),
                keep_alive,
            )
        };

        MqttClient {
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
        while let Some(event) = self
            .client
            .try_next()
            .await
            .map_err(ClientConnectError::PollClientFailure)?
        {
            if let Err(_e) = self.event_handler.handle_event(event).await {
                //warn!(message = "error processing event", error = %e);
            }
        }

        Ok(())
    }

    pub async fn subscribe(&mut self, topics: Vec<String>) -> Result<(), ClientConnectError> {
        debug!("subscribing to topics");
        let subscriptions = topics.iter().map(|topic| proto::SubscribeTo {
            topic_filter: topic.to_string(),
            qos: proto::QoS::AtLeastOnce,
        });

        for subscription in subscriptions {
            debug!("subscribing to topic {}", subscription.topic_filter);
            self.client
                .subscribe(subscription)
                .map_err(ClientConnectError::SubscribeFailure)?;
        }

        let mut subacks: HashSet<_> = topics.iter().collect();

        while let Some(event) = self
            .client
            .try_next()
            .await
            .map_err(ClientConnectError::PollClientFailure)?
        {
            if let Event::SubscriptionUpdates(subscriptions) = event {
                for subscription in subscriptions {
                    if let SubscriptionUpdateEvent::Subscribe(sub) = subscription {
                        subacks.remove(&sub.topic_filter);
                    }
                }

                if subacks.is_empty() {
                    debug!("command handler successfully subscribed to all topics");
                    return Ok(());
                }
            }
        }

        error!("command handler failed to subscribe to topics");
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
    type Error;

    async fn handle_event(&mut self, event: Event) -> Result<(), Self::Error>;
}

#[derive(Debug, thiserror::Error)]
pub enum ClientConnectError {
    #[error("failed to receive expected subacks for command topics: {0:?}")]
    MissingSubacks(Vec<String>),

    #[error("failed to subscribe command handler to command topic")]
    SubscribeFailure(#[from] UpdateSubscriptionError),

    #[error("failed to poll client when validating command handler subscriptions")]
    PollClientFailure(#[from] mqtt3::Error),

    #[error("Failed to shutdown custom mqtt client: {0}")]
    ShutdownClient(#[from] mqtt3::ShutdownError),
}
