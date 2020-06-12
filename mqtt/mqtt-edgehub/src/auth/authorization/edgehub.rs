use std::convert::Infallible;

use mqtt_broker_core::{
    auth::{Activity, AuthId, Authorizer, Connect, Operation, Publish, Subscribe},
    ClientId, ClientInfo,
};

#[derive(Debug, Default)]
pub struct EdgeHubAuthorizer;

impl EdgeHubAuthorizer {
    pub fn new() -> Self {
        Self::default()
    }

    fn authorize_connect(
        &self,
        client_id: &ClientId,
        client_info: &ClientInfo,
        _connect: &Connect,
    ) -> bool {
        match client_info.auth_id() {
            // allow anonymous clients to connect to the broker
            AuthId::Anonymous => true,
            // allow only those clients whose auth_id and client_id identical
            AuthId::Identity(identity) => identity == client_id.as_str(),
        }
    }

    fn authorize_publish(
        &self,
        client_id: &ClientId,
        client_info: &ClientInfo,
        publish: &Publish,
    ) -> bool {
        if self.is_iothub_topic(&publish.publication.topic_name) {
            match client_info.auth_id() {
                // forbid anonymous clients to publish to iothub topics
                AuthId::Anonymous => false,
                // allow only those clients whose auth_id and client_id identical
                AuthId::Identity(identity) => identity == client_id.as_str(),
            }
        } else {
            true
        }
    }

    fn authorize_subscribe(
        &self,
        client_id: &ClientId,
        client_info: &ClientInfo,
        subscribe: &Subscribe,
    ) -> bool {
        if self.is_iothub_topic(&subscribe.topic_filter()) {
            match client_info.auth_id() {
                // forbid anonymous clients to subscribe to iothub topics
                AuthId::Anonymous => false,
                // allow only those clients whose auth_id and client_id identical
                AuthId::Identity(identity) => identity == client_id.as_str(),
            }
        } else {
            true
        }
    }

    fn is_iothub_topic(&self, _topic: &str) -> bool {
        todo!()
    }
}

impl Authorizer for EdgeHubAuthorizer {
    type Error = Infallible;

    fn authorize(&self, activity: Activity) -> Result<bool, Self::Error> {
        let (client_id, client_info, operation) = activity.into_parts();
        let auth = match operation {
            Operation::Connect(connect) => {
                self.authorize_connect(&client_id, &client_info, &connect)
            }
            Operation::Publish(publish) => {
                self.authorize_publish(&client_id, &client_info, &publish)
            }
            Operation::Subscribe(subscribe) => {
                self.authorize_subscribe(&client_id, &client_info, &subscribe)
            }
        };

        Ok(auth)
    }
}

// #[cfg(test)]
// mod tests {
//     use std::time::Duration;

//     use matches::assert_matches;
//     use test_case::test_case;

//     use mqtt3::proto::{self, Publication};
//     use mqtt_broker_core::{
//         auth::{authorize_fn_ok, Activity, AuthId, Authorizer, Operation},
//         ClientInfo,
//     };

//     use super::EdgeHubAuthorizer;

//     #[test_case(connect_activity("127.0.0.1:12345"); "connect")]
//     #[test_case(publish_activity("127.0.0.1:12345"); "publish")]
//     #[test_case(subscribe_activity("127.0.0.1:12345"); "subscribe")]
//     #[test_case(receive_activity("127.0.0.1:12345"); "receive")]
//     fn it_authorizes_client_from_localhost(activity: Activity) {
//         let inner = authorize_fn_ok(|_| false);
//         let authorizer = EdgeHubAuthorizer::new(inner);

//         let auth = authorizer.authorize(activity);

//         assert_matches!(auth, Ok(true));
//     }

//     #[test_case(connect_activity("192.168.0.1:12345"); "connect")]
//     #[test_case(publish_activity("192.168.0.1:12345"); "publish")]
//     #[test_case(subscribe_activity("192.168.0.1:12345"); "subscribe")]
//     #[test_case(receive_activity("192.168.0.1:12345"); "receive")]
//     fn it_calls_inner_authorizer_when_client_not_from_localhost(activity: Activity) {
//         let inner = authorize_fn_ok(|_| false);
//         let authorizer = EdgeHubAuthorizer::new(inner);

//         let auth = authorizer.authorize(activity);

//         assert_matches!(auth, Ok(false));
//     }

//     #[test]
//     fn it_calls_inner_authorizer_for_offline_action_available() {
//         let inner = authorize_fn_ok(|_| false);
//         let authorizer = EdgeHubAuthorizer::new(inner);

//         let publication = Publication {
//             topic_name: "topic".into(),
//             qos: proto::QoS::AtLeastOnce,
//             retain: false,
//             payload: "data".into(),
//         };
//         let operation = Operation::new_receive(publication);
//         let activity = Activity::new_offline("local-client", operation);

//         let auth = authorizer.authorize(activity);

//         assert_matches!(auth, Ok(false));
//     }

//     fn connect_activity(peer_addr: &str) -> Activity {
//         let connect = proto::Connect {
//             username: None,
//             password: None,
//             will: None,
//             client_id: proto::ClientId::IdWithCleanSession("local-client".into()),
//             keep_alive: Duration::from_secs(1),
//             protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
//             protocol_level: mqtt3::PROTOCOL_LEVEL,
//         };

//         let operation = Operation::new_connect(connect);
//         activity(operation, peer_addr)
//     }

//     fn publish_activity(peer_addr: &str) -> Activity {
//         let publish = proto::Publish {
//             packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
//             retain: false,
//             topic_name: "topic".into(),
//             payload: "data".into(),
//         };

//         let operation = Operation::new_publish(publish);
//         activity(operation, peer_addr)
//     }

//     fn subscribe_activity(peer_addr: &str) -> Activity {
//         let subscribe = proto::SubscribeTo {
//             topic_filter: "topic/+".into(),
//             qos: proto::QoS::AtLeastOnce,
//         };

//         let operation = Operation::new_subscribe(subscribe);
//         activity(operation, peer_addr)
//     }

//     fn receive_activity(peer_addr: &str) -> Activity {
//         let publication = Publication {
//             topic_name: "topic".into(),
//             qos: proto::QoS::AtLeastOnce,
//             retain: false,
//             payload: "data".into(),
//         };

//         let operation = Operation::new_receive(publication);
//         activity(operation, peer_addr)
//     }

//     fn activity(operation: Operation, peer_addr: &str) -> Activity {
//         let client_info = ClientInfo::new(
//             peer_addr.parse().expect("peer_addr"),
//             AuthId::Identity("local-client".into()),
//         );
//         Activity::new_active("client-1", client_info, operation)
//     }
// }
