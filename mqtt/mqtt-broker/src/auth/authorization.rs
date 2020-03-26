#![allow(dead_code)]
use async_trait::async_trait;
use mqtt3::proto::{Publication, QoS};

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
}

/// Describes a client operation to be authorized.
pub enum Operation {
    Connect(Connect),
    Publish(Publish),
    Subscribe(Subscribe),
    Receive(Receive),
}

impl Operation {
    pub fn new_connect(will: Option<Publication>) -> Self {
        let will = will.map(|publication| publication.into());
        Self::Connect(Connect { will })
    }
}

/// Represents a client attempt to connect to the broker.
pub struct Connect {
    will: Option<WillPublication>,
}

/// Represents a will publication description to be used for authorization.
pub struct WillPublication {
    pub topic_name: String,
    pub qos: QoS,
    pub retain: bool,
}

impl From<Publication> for WillPublication {
    fn from(publication: Publication) -> Self {
        let Publication {
            topic_name,
            qos,
            retain,
            ..
        } = publication;

        Self {
            topic_name,
            qos,
            retain,
        }
    }
}

/// Represents a client attempt to publish a new message on a specified MQTT topic.
pub struct Publish {
    topic_filter: String,
    retained: bool,
    qos: QoS,
}

/// Represents a client attempt to subscribe to a specified MQTT topic in order to received messages.
pub struct Subscribe {
    topic_filter: String,
    qos: QoS,
}

/// Represents a client to received a message from a specified MQTT topic.
pub struct Receive {
    topic_filter: String,
    qos: QoS,
}

#[cfg(test)]
mod tests {
    use super::*;

    use matches::assert_matches;

    #[tokio::test]
    async fn default_auth_always_deny_any_action() {
        let auth = DefaultAuthorizer;
        let activity = Activity::new("client-auth-id", "client-id", Operation::new_connect(None));

        let res = auth.authorize(activity).await;

        assert_matches!(res, Ok(false));
    }

    #[tokio::test]
    async fn authorizer_wrapper_around_function() {
        let auth = |_| Ok(true);
        let activity = Activity::new("client-auth-id", "client-id", Operation::new_connect(None));

        let res = auth.authorize(activity).await;

        assert_matches!(res, Ok(true));
    }
}
