use std::{any::Any, convert::Infallible, error::Error as StdError};

use mqtt3::proto;

use crate::{ClientId, ClientInfo};

/// A trait to check a MQTT client permissions to perform some actions.
pub trait Authorizer {
    /// Authentication error.
    type Error: StdError;

    /// Authorizes a MQTT client to perform some action.
    fn authorize(&self, activity: Activity) -> Result<Authorization, Self::Error>;

    fn update(&self, _update: Box<dyn Any>) -> Result<(), Self::Error> {
        Ok(())
    }
}

/// Authorization result.
#[derive(Debug, Clone, PartialEq)]
pub enum Authorization {
    Allowed,
    Forbidden(String),
}

/// Creates an authorizer from a function.
/// It wraps any provided function with an interface aligned with authorizer.
pub fn authorize_fn_ok<F>(f: F) -> impl Authorizer
where
    F: Fn(Activity) -> Authorization + Sync + 'static,
{
    move |activity| Ok::<_, Infallible>(f(activity))
}

impl<F, E> Authorizer for F
where
    F: Fn(Activity) -> Result<Authorization, E> + Sync,
    E: StdError,
{
    type Error = E;

    fn authorize(&self, activity: Activity) -> Result<Authorization, Self::Error> {
        self(activity)
    }
}

/// Default implementation that always denies any operation a client intends to perform.
/// This implementation will be used if custom authorization mechanism was not provided.
pub struct DenyAll;

impl Authorizer for DenyAll {
    type Error = Infallible;

    fn authorize(&self, _: Activity) -> Result<Authorization, Self::Error> {
        Ok(Authorization::Forbidden(
            "not allowed by default".to_string(),
        ))
    }
}

/// Default implementation that always allows any operation a client intends to perform.
/// This implementation will be used if custom authorization mechanism was not provided.
pub struct AllowAll;

impl Authorizer for AllowAll {
    type Error = Infallible;

    fn authorize(&self, _: Activity) -> Result<Authorization, Self::Error> {
        Ok(Authorization::Allowed)
    }
}

/// Describes a client activity to authorized.
pub struct Activity {
    client_id: ClientId,
    client_info: ClientInfo,
    operation: Operation,
}

impl Activity {
    pub fn new(
        client_id: impl Into<ClientId>,
        client_info: ClientInfo,
        operation: Operation,
    ) -> Self {
        Self {
            client_id: client_id.into(),
            client_info,
            operation,
        }
    }

    pub fn client_info(&self) -> &ClientInfo {
        &self.client_info
    }

    pub fn operation(&self) -> &Operation {
        &self.operation
    }

    pub fn into_parts(self) -> (ClientId, ClientInfo, Operation) {
        (self.client_id, self.client_info, self.operation)
    }
}

/// Describes a client operation to be authorized.
pub enum Operation {
    Connect(Connect),
    Publish(Publish),
    Subscribe(Subscribe),
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
}

/// Represents a client attempt to connect to the broker.
pub struct Connect {
    will: Option<Publication>,
}

impl Connect {
    pub fn will(&self) -> Option<&Publication> {
        self.will.as_ref()
    }
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

impl Publication {
    pub fn topic_name(&self) -> &str {
        &self.topic_name
    }

    pub fn qos(&self) -> proto::QoS {
        self.qos
    }

    pub fn retain(&self) -> bool {
        self.retain
    }
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

impl Publish {
    pub fn publication(&self) -> &Publication {
        &self.publication
    }
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

    pub fn qos(&self) -> proto::QoS {
        self.qos
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

#[cfg(test)]
mod tests {
    use std::{net::SocketAddr, time::Duration};

    use matches::assert_matches;

    use mqtt3::{proto, PROTOCOL_LEVEL, PROTOCOL_NAME};

    use super::{Activity, AllowAll, Authorization, Authorizer, DenyAll, Operation};
    use crate::ClientInfo;

    fn connect() -> proto::Connect {
        proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::ServerGenerated,
            keep_alive: Duration::from_secs(1),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        }
    }

    #[test]
    fn default_auth_always_deny_any_action() {
        let auth = DenyAll;
        let activity = Activity::new(
            "client-auth-id",
            ClientInfo::new(peer_addr(), "client-id"),
            Operation::new_connect(connect()),
        );

        let res = auth.authorize(activity);

        assert_matches!(res, Ok(Authorization::Forbidden(_)));
    }

    #[test]
    fn authorizer_wrapper_around_function() {
        let auth = AllowAll;
        let activity = Activity::new(
            "client-auth-id",
            ClientInfo::new(peer_addr(), "client-id"),
            Operation::new_connect(connect()),
        );

        let res = auth.authorize(activity);

        assert_matches!(res, Ok(Authorization::Allowed));
    }

    fn peer_addr() -> SocketAddr {
        "127.0.0.1:12345".parse().unwrap()
    }
}
