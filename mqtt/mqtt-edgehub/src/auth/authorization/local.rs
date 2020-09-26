use std::convert::Infallible;

use mqtt_broker::auth::{Activity, Authorization, Authorizer};

/// `LocalAuthorizer` implicitly allows all operations that come from local clients. Local
/// clients are those with peer ip address equal to loop back (localhost).
///
/// It denies all other non localhost operations.
///
/// This is the first authorizer in the chain of edgehub-specific authorizers (see `EdgeHubAuthorizer`).
/// It's purpose to allow sidecars (`CommandHandler`, `Bridge`, `EdgeHub bridge`, etc...) to connect
/// before public external transport is available for all other clients to connect.
#[derive(Copy, Clone)]
pub struct LocalAuthorizer;

impl Authorizer for LocalAuthorizer {
    type Error = Infallible;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        if activity.client_info().peer_addr().ip().is_loopback() {
            return Ok(Authorization::Allowed);
        }
        Ok(Authorization::Forbidden("non-local client".into()))
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use test_case::test_case;

    use mqtt3::proto;
    use mqtt_broker::{
        auth::{Activity, AuthId, Authorization, Authorizer, Operation},
        ClientInfo,
    };

    use super::LocalAuthorizer;

    #[test_case(&connect_activity("127.0.0.1:12345"), Authorization::Allowed; "local connect")]
    #[test_case(&publish_activity("127.0.0.1:12345"), Authorization::Allowed; "local publish")]
    #[test_case(&subscribe_activity("127.0.0.1:12345"), Authorization::Allowed; "local subscribe")]
    #[test_case(&connect_activity("192.168.0.1:12345"), Authorization::Forbidden("non-local client".into()); "remote connect")]
    #[test_case(&publish_activity("192.168.0.1:12345"), Authorization::Forbidden("non-local client".into()); "remote publish")]
    #[test_case(&subscribe_activity("192.168.0.1:12345"), Authorization::Forbidden("non-local client".into()); "remote subscribe")]
    fn it_authorizes_client_from_localhost(activity: &Activity, result: Authorization) {
        let auth = LocalAuthorizer.authorize(&activity);

        assert_eq!(auth, Ok(result));
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
