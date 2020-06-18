use std::error::Error as StdError;

use mqtt_broker_core::auth::{Activity, Authorization, Authorizer};

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

    fn authorize(&self, activity: Activity) -> Result<Authorization, Self::Error> {
        if activity.client_info().peer_addr().ip().is_loopback() {
            return Ok(Authorization::Allowed);
        }

        self.0.authorize(activity)
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use matches::assert_matches;
    use test_case::test_case;

    use mqtt3::proto;
    use mqtt_broker_core::{
        auth::{authorize_fn_ok, Activity, AuthId, Authorization, Authorizer, Operation},
        ClientInfo,
    };

    use super::LocalAuthorizer;

    #[test_case(connect_activity("127.0.0.1:12345"); "connect")]
    #[test_case(publish_activity("127.0.0.1:12345"); "publish")]
    #[test_case(subscribe_activity("127.0.0.1:12345"); "subscribe")]
    fn it_authorizes_client_from_localhost(activity: Activity) {
        let inner = authorize_fn_ok(|_| Authorization::Forbidden("forbid everything".to_string()));
        let authorizer = LocalAuthorizer::new(inner);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(connect_activity("192.168.0.1:12345"); "connect")]
    #[test_case(publish_activity("192.168.0.1:12345"); "publish")]
    #[test_case(subscribe_activity("192.168.0.1:12345"); "subscribe")]
    fn it_calls_inner_authorizer_when_client_not_from_localhost(activity: Activity) {
        let inner = authorize_fn_ok(|_| Authorization::Forbidden("not allowed".to_string()));
        let authorizer = LocalAuthorizer::new(inner);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(auth) if auth == Authorization::Forbidden("not allowed".to_string()));
    }

    fn connect_activity(peer_addr: &str) -> Activity {
        let connect = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("local-client".into()),
            keep_alive: Duration::from_secs(1),
            protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
            protocol_level: mqtt3::PROTOCOL_LEVEL,
        };

        let operation = Operation::new_connect(connect);
        activity(operation, peer_addr)
    }

    fn publish_activity(peer_addr: &str) -> Activity {
        let publish = proto::Publish {
            packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
            retain: false,
            topic_name: "topic".into(),
            payload: "data".into(),
        };

        let operation = Operation::new_publish(publish);
        activity(operation, peer_addr)
    }

    fn subscribe_activity(peer_addr: &str) -> Activity {
        let subscribe = proto::SubscribeTo {
            topic_filter: "topic/+".into(),
            qos: proto::QoS::AtLeastOnce,
        };

        let operation = Operation::new_subscribe(subscribe);
        activity(operation, peer_addr)
    }

    fn activity(operation: Operation, peer_addr: &str) -> Activity {
        let client_info = ClientInfo::new(
            peer_addr.parse().expect("peer_addr"),
            AuthId::Identity("local-client".into()),
        );
        Activity::new("client-1", client_info, operation)
    }
}
