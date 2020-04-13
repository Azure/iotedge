#![allow(dead_code)]
use async_trait::async_trait;
use mqtt3::proto;

use crate::{AuthId, ClientId, Error};

/// A trait to check a MQTT client permissions to perform some actions.
#[async_trait]
pub trait Authorizer {
    /// Authorizes a MQTT client to perform some action.
    async fn authorize(&self, activity: Activity) -> Result<bool, Error>;
}

#[async_trait]
impl<F> Authorizer for F
where
    F: Fn(Activity) -> Result<bool, Error> + Sync,
{
    async fn authorize(&self, activity: Activity) -> Result<bool, Error> {
        self(activity)
    }
}

/// Default implementation that always denies any operation a client intends to perform.
/// This implementation will be used if custom authorization mechanism was not provided.
pub struct DefaultAuthorizer;

#[async_trait]
impl Authorizer for DefaultAuthorizer {
    async fn authorize(&self, _: Activity) -> Result<bool, Error> {
        Ok(false)
    }
}

/// Describes a client activity to authorized.
pub struct Activity {
    auth_id: AuthId,
    client_id: ClientId,
    operation: Operation,
}

impl Activity {
    pub fn new(
        auth_id: impl Into<AuthId>,
        client_id: impl Into<ClientId>,
        operation: Operation,
    ) -> Self {
        Self {
            auth_id: auth_id.into(),
            client_id: client_id.into(),
            operation,
        }
    }

    pub fn operation(&self) -> &Operation {
        &self.operation
    }
}

/// Describes a client operation to be authorized.
pub enum Operation {
    Connect(Connect),
    Publish(Publish),
    Subscribe(Subscribe),
    Receive(Receive),
}

impl Operation {
    /// Creates a new operation context for CONNECT request.
    pub fn new_connect(connect: proto::Connect) -> Self {
        Self::Connect(connect.into())
    }

    /// Creates a new operation context for PUBLISH request.
    pub fn new_publish(publish: proto::Publish) -> Self {
        Self::Publish(publish.into())
    }

    /// Creates a new operation context for SUBSCRIBE request.
    pub fn new_subscribe(subscribe_to: proto::SubscribeTo) -> Self {
        Self::Subscribe(subscribe_to.into())
    }

    /// Creates a new operation context for RECEIVE request.
    ///
    /// RECEIVE request happens when broker decides to publish a message to a certain
    /// topic client subscribed to.
    pub fn new_receive(publication: proto::Publication) -> Self {
        Self::Receive(publication.into())
    }
}

/// Represents a client attempt to connect to the broker.
pub struct Connect {
    will: Option<Publication>,
}

impl From<proto::Connect> for Connect {
    fn from(connect: proto::Connect) -> Self {
        Self {
            will: connect.will.map(Into::into),
        }
    }
}

/// Represents a publication description without payload to be used for authorization.
pub struct Publication {
    topic_name: String,
    qos: proto::QoS,
    retain: bool,
}

impl From<proto::Publication> for Publication {
    fn from(publication: proto::Publication) -> Self {
        Self {
            topic_name: publication.topic_name,
            qos: publication.qos,
            retain: publication.retain,
        }
    }
}

/// Represents a client attempt to publish a new message on a specified MQTT topic.
pub struct Publish {
    publication: Publication,
}

impl From<proto::Publish> for Publish {
    fn from(publish: proto::Publish) -> Self {
        Self {
            publication: Publication {
                topic_name: publish.topic_name,
                qos: match publish.packet_identifier_dup_qos {
                    proto::PacketIdentifierDupQoS::AtMostOnce => proto::QoS::AtMostOnce,
                    proto::PacketIdentifierDupQoS::AtLeastOnce(_, _) => proto::QoS::AtLeastOnce,
                    proto::PacketIdentifierDupQoS::ExactlyOnce(_, _) => proto::QoS::ExactlyOnce,
                },
                retain: publish.retain,
            },
        }
    }
}

/// Represents a client attempt to subscribe to a specified MQTT topic in order to received messages.
pub struct Subscribe {
    topic_filter: String,
    qos: proto::QoS,
}

impl Subscribe {
    pub fn topic_filter(&self) -> &str {
        &self.topic_filter
    }
}

impl From<proto::SubscribeTo> for Subscribe {
    fn from(subscribe_to: proto::SubscribeTo) -> Self {
        Self {
            topic_filter: subscribe_to.topic_filter,
            qos: subscribe_to.qos,
        }
    }
}

/// Represents a client to received a message from a specified MQTT topic.
pub struct Receive {
    publication: Publication,
}

impl From<proto::Publication> for Receive {
    fn from(publication: proto::Publication) -> Self {
        Self {
            publication: publication.into(),
        }
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use matches::assert_matches;

    use super::*;

    fn connect() -> mqtt3::proto::Connect {
        proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::ServerGenerated,
            keep_alive: Duration::from_secs(1),
            protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
            protocol_level: mqtt3::PROTOCOL_LEVEL,
        }
    }

    #[tokio::test]
    async fn default_auth_always_deny_any_action() {
        let auth = DefaultAuthorizer;
        let activity = Activity::new(
            "client-auth-id",
            "client-id",
            Operation::new_connect(connect()),
        );

        let res = auth.authorize(activity).await;

        assert_matches!(res, Ok(false));
    }

    #[tokio::test]
    async fn authorizer_wrapper_around_function() {
        let auth = |_| Ok(true);
        let activity = Activity::new(
            "client-auth-id",
            "client-id",
            Operation::new_connect(connect()),
        );

        let res = auth.authorize(activity).await;

        assert_matches!(res, Ok(true));
    }
}
