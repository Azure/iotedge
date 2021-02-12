#![allow(clippy::default_trait_access)] // Needed because mock! macro violates
use core::{convert::TryInto, num::TryFromIntError};
use std::{fmt::Display, str, time::Duration};

use async_trait::async_trait;
use mockall::automock;
use tokio::stream::StreamExt;
use tracing::{debug, error, info};

use mqtt3::{
    proto::{self, Publication, SubscribeTo},
    Client, Event, ShutdownError, UpdateSubscriptionError,
};
use mqtt_util::{ClientIoSource, Credentials, SasTokenSource, TcpConnection, TrustBundleSource};

const DEFAULT_MAX_RECONNECT: Duration = Duration::from_secs(60);
// TODO: get QOS from topic settings
const DEFAULT_QOS: proto::QoS = proto::QoS::AtLeastOnce;
// TODO: read from env var
const API_VERSION: &str = "2010-01-01";

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
    client: Client<ClientIoSource>,
    event_handler: H,
}

impl<H> MqttClient<H>
where
    H: MqttEventHandler,
{
    pub fn tcp(config: MqttClientConfig, event_handler: H) -> Result<Self, ClientError> {
        let token_source = Self::token_source(&config.credentials);
        let tcp_connection = TcpConnection::new(config.addr, token_source, None);
        let io_source = ClientIoSource::Tcp(tcp_connection);

        Self::new(
            config.keep_alive,
            config.clean_session,
            event_handler,
            &config.credentials,
            io_source,
        )
    }

    pub fn tls(config: MqttClientConfig, event_handler: H) -> Result<Self, ClientError> {
        let trust_bundle = Some(TrustBundleSource::new(config.credentials.clone()));

        let token_source = Self::token_source(&config.credentials);
        let tcp_connection = TcpConnection::new(config.addr, token_source, trust_bundle);
        let io_source = ClientIoSource::Tls(tcp_connection);

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
        io_source: ClientIoSource,
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

        Self::validate(client_id.as_ref(), username.as_ref(), &keep_alive)?;

        let client = Client::new(
            client_id,
            username,
            None,
            io_source,
            DEFAULT_MAX_RECONNECT,
            keep_alive,
        );

        Ok(Self {
            client,
            event_handler,
        })
    }

    fn validate(
        client_id: Option<&String>,
        username: Option<&String>,
        keep_alive: &Duration,
    ) -> Result<(), ClientError> {
        if let Some(id) = client_id {
            validate_length(id)?;
        };

        if let Some(name) = username {
            validate_length(name)?;
        }

        let _: u16 = keep_alive
            .as_secs()
            .try_into()
            .map_err(ClientError::StringTooLarge)?;

        Ok(())
    }

    fn token_source(connection_credentials: &Credentials) -> Option<SasTokenSource> {
        match connection_credentials {
            Credentials::Provider(_) | Credentials::PlainText(_) => {
                Some(SasTokenSource::new(connection_credentials.clone()))
            }
            Credentials::Anonymous(_) => None,
        }
    }

    pub async fn run(&mut self) -> Result<(), ClientError> {
        let default_topics = self.event_handler.subscriptions();
        self.subscribe(default_topics).await?;

        self.handle_events().await?;
        Ok(())
    }

    async fn handle_events(&mut self) -> Result<(), ClientError> {
        debug!("polling bridge client...");

        while let Some(event) = self.client.try_next().await? {
            debug!("handling event {:?}", event);
            if let Err(e) = self.event_handler.handle(event).await {
                error!(error = %e, "error processing event");
            }
        }

        Ok(())
    }

    async fn subscribe(&mut self, topics: Vec<String>) -> Result<(), ClientError> {
        info!("subscribing to topics {:?}...", topics);
        let subscriptions = topics.into_iter().map(|topic| proto::SubscribeTo {
            topic_filter: topic,
            qos: DEFAULT_QOS,
        });

        for subscription in subscriptions {
            self.client
                .subscribe(subscription)
                .map_err(ClientError::Subscribe)?;
        }

        Ok(())
    }
}

fn validate_length(id: &str) -> Result<(), ClientError> {
    let _: u16 = id.len().try_into().map_err(ClientError::StringTooLarge)?;

    Ok(())
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
        let publish_handle = self
            .client
            .publish_handle()
            .map_err(ClientError::PublishHandle)?;
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
    pub async fn publish(&mut self, publication: Publication) -> Result<(), ClientError> {
        self.0
            .publish(publication)
            .await
            .map_err(ClientError::PublishError)
    }
}

mockall::mock! {
    pub PublishHandle {
        async fn publish(&mut self, publication: Publication) -> Result<(), ClientError>;
    }

    pub trait Clone {
        fn clone(&self) -> Self;
    }
}

/// A client subscription update handle.
pub struct UpdateSubscriptionHandle(mqtt3::UpdateSubscriptionHandle);

#[automock]
impl UpdateSubscriptionHandle {
    pub async fn subscribe(&mut self, subscribe_to: SubscribeTo) -> Result<(), ClientError> {
        self.0
            .subscribe(subscribe_to)
            .await
            .map_err(ClientError::UpdateSubscriptionError)
    }

    pub async fn unsubscribe(&mut self, unsubscribe_from: String) -> Result<(), ClientError> {
        self.0
            .unsubscribe(unsubscribe_from)
            .await
            .map_err(ClientError::UpdateSubscriptionError)
    }
}

/// A trait which every MQTT client event handler implements.
#[async_trait]
pub trait MqttEventHandler {
    type Error: Display;

    /// Returns a list of subscriptions for a MQTT client to subscribe to
    /// when client starts.
    fn subscriptions(&self) -> Vec<String> {
        vec![]
    }

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
    #[error("failed to subscribe topic. Caused by: {0}")]
    Subscribe(#[from] UpdateSubscriptionError),

    #[error("failed to poll client. Caused by: {0}")]
    PollClient(#[from] mqtt3::Error),

    #[error("failed to shutdown custom mqtt client. Caused by: {0}")]
    ShutdownClient(#[from] mqtt3::ShutdownError),

    #[error("failed to obtain publish handle. Caused by: {0}")]
    PublishHandle(#[source] mqtt3::PublishError),

    #[error("failed to publish event. Caused by: {0}")]
    PublishError(#[source] mqtt3::PublishError),

    #[error("failed to obtain subscribe handle. Caused by: {0}")]
    UpdateSubscriptionHandle(#[source] mqtt3::UpdateSubscriptionError),

    #[error("failed to send update subscription. Caused by: {0}")]
    UpdateSubscriptionError(#[source] mqtt3::UpdateSubscriptionError),

    #[error("failed to connect")]
    SslHandshake,

    #[error("string too large. Caused by: {0}")]
    StringTooLarge(#[from] TryFromIntError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use mqtt_util::AuthenticationSettings;

    #[derive(Default)]
    struct EventHandler {}

    #[async_trait]
    impl MqttEventHandler for EventHandler {
        type Error = ClientError;

        async fn handle(&mut self, _event: Event) -> Result<Handled, Self::Error> {
            Ok(Handled::Fully)
        }
    }

    #[test]
    fn new_validates_valid_input() {
        let addr = "addr".to_owned();
        let secs: u64 = (u16::MAX).into();
        let keep_alive = Duration::from_secs(secs);
        let clean_session = true;
        let connection_credentials = Credentials::Anonymous("user".to_owned());

        let event_handler = EventHandler::default();

        let client = MqttClient::new(
            keep_alive,
            clean_session,
            event_handler,
            &connection_credentials,
            ClientIoSource::Tcp(TcpConnection::new(addr, None, None)),
        );

        assert_eq!(client.is_ok(), true);
    }

    #[test]
    fn new_validates_invalid_keep_alive() {
        let addr = "addr".to_owned();
        let secs: u64 = (u16::MAX).into();
        let keep_alive = Duration::from_secs(secs + 1);
        let clean_session = true;
        let connection_credentials = Credentials::Anonymous("user".to_owned());

        let event_handler = EventHandler::default();
        let client = MqttClient::new(
            keep_alive,
            clean_session,
            event_handler,
            &connection_credentials,
            ClientIoSource::Tcp(TcpConnection::new(addr, None, None)),
        );

        assert_eq!(client.is_err(), true);
    }

    #[test]
    fn new_validates_invalid_client_id() {
        let addr = "addr".to_owned();
        let keep_alive = Duration::from_secs(120);
        let clean_session = false;
        let repeat: usize = (u16::MAX).into();
        let connection_credentials = Credentials::PlainText(AuthenticationSettings::new(
            "c".repeat(repeat + 1),
            "username".into(),
            "pass".into(),
            None,
        ));

        let event_handler = EventHandler::default();

        let client = MqttClient::new(
            keep_alive,
            clean_session,
            event_handler,
            &connection_credentials,
            ClientIoSource::Tcp(TcpConnection::new(addr, None, None)),
        );

        assert_eq!(client.is_err(), true);
    }

    #[test]
    fn new_validates_invalid_username() {
        let addr = "addr".to_owned();
        let keep_alive = Duration::from_secs(120);
        let clean_session = false;
        let repeat: usize = (u16::MAX).into();
        let connection_credentials = Credentials::PlainText(AuthenticationSettings::new(
            "user".into(),
            "u".repeat(repeat + 1),
            "pass".into(),
            None,
        ));

        let event_handler = EventHandler::default();

        let client = MqttClient::new(
            keep_alive,
            clean_session,
            event_handler,
            &connection_credentials,
            ClientIoSource::Tcp(TcpConnection::new(addr, None, None)),
        );

        assert_eq!(client.is_err(), true);
    }
}
