use std::{
    any::Any,
    convert::Infallible,
    error::Error as StdError,
    fmt::{Display, Formatter, Result as FmtResult},
};

use mqtt3::proto;

use crate::{ClientId, ClientInfo};

/// A trait to check a MQTT client permissions to perform some actions.
pub trait Authorizer {
    /// Authentication error.
    type Error: StdError;

    /// Authorizes a MQTT client to perform some action.
    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error>;

    fn update(&mut self, _update: Box<dyn Any>) -> Result<(), Self::Error> {
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
    F: Fn(&Activity) -> Authorization + Sync + 'static,
{
    move |activity: &Activity| Ok::<_, Infallible>(f(activity))
}

impl<F, E> Authorizer for F
where
    F: Fn(&Activity) -> Result<Authorization, E> + Sync,
    E: StdError,
{
    type Error = E;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        self(activity)
    }
}

/// Default implementation that always denies any operation a client intends to perform.
/// This implementation will be used if custom authorization mechanism was not provided.
pub struct DenyAll;

impl Authorizer for DenyAll {
    type Error = Infallible;

    fn authorize(&self, _: &Activity) -> Result<Authorization, Self::Error> {
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

    fn authorize(&self, _: &Activity) -> Result<Authorization, Self::Error> {
        Ok(Authorization::Allowed)
    }
}

/// Describes a client activity to authorized.
#[derive(Clone, Debug)]
pub struct Activity {
    client_info: ClientInfo,
    operation: Operation,
}

impl Activity {
    pub fn new(client_info: ClientInfo, operation: Operation) -> Self {
        Self {
            client_info,
            operation,
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_info.client_id()
    }

    pub fn client_info(&self) -> &ClientInfo {
        &self.client_info
    }

    pub fn operation(&self) -> &Operation {
        &self.operation
    }

    pub fn into_parts(self) -> (ClientInfo, Operation) {
        (self.client_info, self.operation)
    }
}

impl Display for Activity {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(
            f,
            "client: {} operation: {}",
            self.client_id(),
            self.operation()
        )
    }
}

/// Describes a client operation to be authorized.
#[derive(Clone, Debug)]
pub enum Operation {
    Connect,
    Publish(Publish),
    Subscribe(Subscribe),
}

impl Operation {
    /// Creates a new operation context for CONNECT request.
    pub fn new_connect() -> Self {
        Self::Connect
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

impl Display for Operation {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            Self::Connect => write!(f, "CONNECT"),
            Self::Publish(publish) => write!(f, "PUBLISH {}", publish.publication.topic_name),
            Self::Subscribe(subscribe) => write!(
                f,
                "SUBSCRIBE {}; qos: {}",
                subscribe.topic_filter,
                u8::from(subscribe.qos)
            ),
        }
    }
}

/// Represents a publication description without payload to be used for authorization.
#[derive(Clone, Debug)]
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
#[derive(Clone, Debug)]
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
#[derive(Clone, Debug)]
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
    use std::net::SocketAddr;

    use matches::assert_matches;

    use super::{Activity, AllowAll, Authorization, Authorizer, DenyAll, Operation};
    use crate::ClientInfo;

    #[test]
    fn default_auth_always_deny_any_action() {
        let auth = DenyAll;
        let activity = Activity::new(
            ClientInfo::new("client-auth-id", peer_addr(), "client-id"),
            Operation::new_connect(),
        );

        let res = auth.authorize(&activity);

        assert_matches!(res, Ok(Authorization::Forbidden(_)));
    }

    #[test]
    fn authorizer_wrapper_around_function() {
        let auth = AllowAll;
        let activity = Activity::new(
            ClientInfo::new("client-auth-id", peer_addr(), "client-id"),
            Operation::new_connect(),
        );

        let res = auth.authorize(&activity);

        assert_matches!(res, Ok(Authorization::Allowed));
    }

    fn peer_addr() -> SocketAddr {
        "127.0.0.1:12345".parse().unwrap()
    }
}
