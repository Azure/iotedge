use std::{any::Any, error::Error as StdError};

use mqtt_broker::auth::{Activity, Authorization, Authorizer};

/// `LocalAuthorizer` implicitly allows all operations that come from local clients. Local
/// clients are those with peer ip address equal to loop back (localhost).
///
/// For non-local clients it delegates the request to an inner authorizer.
///
/// This is the first authorizer in the chain of edgehub-specific authorizers.
/// It's purpose to allow sidecars (`CommandHandler`, `Bridge`, `EdgeHub bridge`, etc...) to connect
/// before public external transport is available for all other clients to connect.
#[derive(Debug, Copy, Clone)]
pub struct LocalAuthorizer<Z>(Z);

impl<Z> LocalAuthorizer<Z>
where
    Z: Authorizer,
{
    pub fn new(authorizer: Z) -> Self {
        Self(authorizer)
    }
}

impl<Z, E> Authorizer for LocalAuthorizer<Z>
where
    Z: Authorizer<Error = E>,
    E: StdError,
{
    type Error = E;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        if activity.client_info().peer_addr().ip().is_loopback() {
            return Ok(Authorization::Allowed);
        }

        self.0.authorize(activity)
    }

    fn update(&mut self, update: Box<dyn Any>) -> Result<(), Self::Error> {
        self.0.update(update)
    }
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;
    use test_case::test_case;

    use mqtt3::proto;
    use mqtt_broker::{
        auth::{authorize_fn_ok, Activity, AuthId, Authorization, Authorizer, DenyAll, Operation},
        ClientId, ClientInfo,
    };

    use super::LocalAuthorizer;

    #[test_case(&connect_activity("127.0.0.1:12345"); "connect")]
    #[test_case(&publish_activity("127.0.0.1:12345"); "publish")]
    #[test_case(&subscribe_activity("127.0.0.1:12345"); "subscribe")]
    fn it_authorizes_client_from_localhost(activity: &Activity) {
        let authorizer = LocalAuthorizer::new(DenyAll);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&connect_activity("192.168.0.1:12345"); "connect")]
    #[test_case(&publish_activity("192.168.0.1:12345"); "publish")]
    #[test_case(&subscribe_activity("192.168.0.1:12345"); "subscribe")]
    fn it_calls_inner_authorizer_when_client_not_from_localhost(activity: &Activity) {
        let inner = authorize_fn_ok(|_| Authorization::Forbidden("not allowed inner".to_string()));
        let authorizer = LocalAuthorizer::new(inner);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(auth) if auth == Authorization::Forbidden("not allowed inner".to_string()));
    }

    fn connect_activity(peer_addr: &str) -> Activity {
        let operation = Operation::new_connect();
        activity("client_id".into(), operation, peer_addr)
    }

    fn publish_activity(peer_addr: &str) -> Activity {
        let publish = proto::Publish {
            packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
            retain: false,
            topic_name: "topic".into(),
            payload: "data".into(),
        };

        let operation = Operation::new_publish(publish);
        activity("client_id".into(), operation, peer_addr)
    }

    fn subscribe_activity(peer_addr: &str) -> Activity {
        let subscribe = proto::SubscribeTo {
            topic_filter: "topic/+".into(),
            qos: proto::QoS::AtLeastOnce,
        };

        let operation = Operation::new_subscribe(subscribe);
        activity("client_id".into(), operation, peer_addr)
    }

    fn activity(client_id: ClientId, operation: Operation, peer_addr: &str) -> Activity {
        let client_info = ClientInfo::new(
            client_id,
            peer_addr.parse().expect("peer_addr"),
            AuthId::Identity("local-client".into()),
        );
        Activity::new(client_info, operation)
    }
}
