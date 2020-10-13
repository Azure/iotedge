use std::collections::HashMap;

use async_trait::async_trait;
use bson::{doc, Document};
use bytes::{buf::BufExt, Bytes};
use lazy_static::lazy_static;
use regex::Regex;
use serde::{Deserialize, Serialize};
use tracing::{error, warn};

// Import and use mocks when run tests, real implementation when otherwise
#[cfg(test)]
pub use mqtt3::{
    MockPublishHandle as PublishHandle, MockUpdateSubscriptionHandle as UpdateSubscriptionHandle,
};
#[cfg(not(test))]
pub use mqtt3::{PublishHandle, UpdateSubscriptionHandle};

use mqtt3::{
    proto::Publication, proto::QoS, proto::SubscribeTo, Event, PublishError,
    SubscriptionUpdateEvent,
};

use crate::client::{EventHandler, Handled};

/// MQTT client event handler to react on RPC commands that `EdgeHub` sends
/// to execute.
///
/// The main purpose of this handler is to establish a communication channel
/// between `EdgeHub` and the upstream bridge.
/// `EdgeHub` will use low level commands SUB, UNSUB, PUB. In turn the bridge
/// sends corresponding MQTT packet to upstream broker and waits for an ack
/// from the upstream. After ack is received it sends a special publish to
/// downstream broker.
pub struct RpcHandler {
    upstream_subs: UpdateSubscriptionHandle,
    upstream_pubs: PublishHandle,
    downstream_pubs: PublishHandle,
    subscriptions: HashMap<String, String>,
}

impl RpcHandler {
    #[allow(dead_code)] // TODO remove when used in the code
    pub fn new(
        upstream_subs: UpdateSubscriptionHandle,
        upstream_pubs: PublishHandle,
        downstream_pubs: PublishHandle,
    ) -> Self {
        Self {
            upstream_subs,
            upstream_pubs,
            downstream_pubs,
            subscriptions: HashMap::default(),
        }
    }

    async fn handle_command(
        &mut self,
        command_id: String,
        command: RpcCommand,
    ) -> Result<(), RpcError> {
        match command {
            RpcCommand::Subscribe { topic_filter } => {
                self.handle_subscribe(command_id, topic_filter).await
            }
            RpcCommand::Unsubscribe { topic_filter } => {
                self.handle_unsubscribe(command_id, topic_filter).await
            }
            RpcCommand::Publish { topic, payload } => {
                self.handle_publish(command_id, topic, payload).await
            }
        }
    }

    async fn handle_subscribe(
        &mut self,
        command_id: String,
        topic_filter: String,
    ) -> Result<(), RpcError> {
        let subscribe_to = SubscribeTo {
            topic_filter: topic_filter.clone(),
            qos: QoS::AtLeastOnce,
        };

        match self.upstream_subs.subscribe(subscribe_to).await {
            Ok(_) => {
                if let Some(existing) = self.subscriptions.insert(topic_filter, command_id) {
                    warn!("duplicating sub request found for {}", existing);
                }
            }
            Err(e) => {
                let reason = format!("unable to subscribe to upstream {}. {}", topic_filter, e);
                self.publish_nack(command_id, reason).await?;
            }
        }

        Ok(())
    }

    async fn handle_unsubscribe(
        &mut self,
        command_id: String,
        topic_filter: String,
    ) -> Result<(), RpcError> {
        match self.upstream_subs.unsubscribe(topic_filter.clone()).await {
            Ok(_) => {
                if let Some(existing) = self.subscriptions.insert(topic_filter, command_id) {
                    warn!("duplicating unsub request found for {}", existing);
                }
            }
            Err(e) => {
                let reason = format!(
                    "unable to unsubscribe from upstream {}. {}",
                    topic_filter, e
                );
                self.publish_nack(command_id, reason).await?;
            }
        }

        Ok(())
    }

    async fn handle_publish(
        &mut self,
        command_id: String,
        topic_name: String,
        payload: Vec<u8>,
    ) -> Result<(), RpcError> {
        let publication = Publication {
            topic_name: topic_name.clone(),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: payload.into(),
        };

        match self.upstream_pubs.publish(publication).await {
            Ok(_) => self.publish_ack(command_id).await,
            Err(e) => {
                let reason = format!("unable to publish to upstream {}. {}", topic_name, e);
                self.publish_nack(command_id, reason).await
            }
        }
    }

    async fn handle_subcription_update(
        &mut self,
        subscription: &SubscriptionUpdateEvent,
    ) -> Result<bool, RpcError> {
        match subscription {
            SubscriptionUpdateEvent::Subscribe(sub) => {
                if let Some(command_id) = self.subscriptions.remove(&sub.topic_filter) {
                    self.publish_ack(command_id).await?;
                    return Ok(true);
                }
            }
            SubscriptionUpdateEvent::RejectedByServer(topic_filter) => {
                if let Some(command_id) = self.subscriptions.remove(topic_filter) {
                    let reason = format!("subscription rejected by server {}", topic_filter);
                    self.publish_nack(command_id, reason).await?;
                    return Ok(true);
                }
            }
            SubscriptionUpdateEvent::Unsubscribe(topic_filter) => {
                if let Some(command_id) = self.subscriptions.remove(topic_filter) {
                    self.publish_ack(command_id).await?;
                    return Ok(true);
                }
            }
        }

        Ok(false)
    }

    async fn publish_nack(&mut self, command_id: String, reason: String) -> Result<(), RpcError> {
        let mut payload = Vec::new();
        let doc = doc! { "reason": reason };
        doc.to_writer(&mut payload)?;

        let publication = Publication {
            topic_name: format!("$edgehub/rpc/nack/{}", &command_id),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: payload.into(),
        };

        self.downstream_pubs
            .publish(publication)
            .await
            .map_err(|e| RpcError::SendNack(command_id, e))
    }

    async fn publish_ack(&mut self, command_id: String) -> Result<(), RpcError> {
        let publication = Publication {
            topic_name: format!("$edgehub/rpc/ack/{}", &command_id),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: Bytes::default(),
        };

        self.downstream_pubs
            .publish(publication)
            .await
            .map_err(|e| RpcError::SendAck(command_id, e))
    }
}

#[async_trait(?Send)]
impl EventHandler for RpcHandler {
    type Error = RpcError;

    async fn handle(&mut self, event: &Event) -> Result<Handled, Self::Error> {
        match event {
            Event::Publication(publication) => {
                if let Some(command_id) = capture_command_id(&publication.topic_name) {
                    let doc = Document::from_reader(&mut publication.payload.clone().reader())?;
                    match bson::from_document(doc)? {
                        VersionedRpcCommand::V1(command) => {
                            self.handle_command(command_id, command).await?;

                            return Ok(Handled::Fully);
                        }
                    }
                }
            }
            Event::SubscriptionUpdates(subscriptions) => {
                let mut handled = 0;
                for subscription in subscriptions {
                    if self.handle_subcription_update(subscription).await? {
                        handled += 1;
                    }
                }

                if handled == subscriptions.len() {
                    return Ok(Handled::Fully);
                } else {
                    return Ok(Handled::Partially);
                }
            }
            _ => {}
        }

        Ok(Handled::Skipped)
    }
}

#[derive(Debug, thiserror::Error)]
pub enum RpcError {
    #[error("failed to deserialize command from received publication")]
    DeserializeCommand(#[from] bson::de::Error),

    #[error("failed to serialize ack")]
    SerializeAck(#[from] bson::ser::Error),

    #[error("unable to send nack for {0}. {1}")]
    SendNack(String, #[source] PublishError),

    #[error("unable to send ack for {0}. {1}")]
    SendAck(String, #[source] PublishError),
}

fn capture_command_id(topic_name: &str) -> Option<String> {
    lazy_static! {
        static ref RPC_TOPIC_PATTERN: Regex = Regex::new("\\$edgehub/rpc/(?P<command_id>[^/ ]+)$")
            .expect("failed to create new Regex from pattern");
    }

    RPC_TOPIC_PATTERN
        .captures(topic_name)
        .and_then(|captures| captures.name("command_id"))
        .map(|command_id| command_id.as_str().into())
}

#[derive(Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", tag = "cmd")]
enum RpcCommand {
    #[serde(rename = "sub")]
    Subscribe {
        #[serde(rename = "topic")]
        topic_filter: String,
    },

    #[serde(rename = "unsub")]
    Unsubscribe {
        #[serde(rename = "topic")]
        topic_filter: String,
    },

    #[serde(rename = "pub")]
    Publish {
        topic: String,

        #[serde(with = "serde_bytes")]
        payload: Vec<u8>,
    },
}

#[derive(Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", tag = "version")]
enum VersionedRpcCommand {
    V1(RpcCommand),
}

#[cfg(test)]
mod tests {
    use bson::{bson, doc, spec::BinarySubtype};
    use matches::assert_matches;
    use mqtt3::{
        proto::QoS, Event, MockPublishHandle, MockUpdateSubscriptionHandle, ReceivedPublication,
    };
    use test_case::test_case;

    use super::*;

    #[test]
    fn it_deserializes_from_bson() {
        let commands = vec![
            (
                bson!({
                    "version": "v1",
                    "cmd": "sub",
                    "topic": "/foo",
                }),
                VersionedRpcCommand::V1(RpcCommand::Subscribe {
                    topic_filter: "/foo".into(),
                }),
            ),
            (
                bson!({
                    "version": "v1",
                    "cmd": "unsub",
                    "topic": "/foo",
                }),
                VersionedRpcCommand::V1(RpcCommand::Unsubscribe {
                    topic_filter: "/foo".into(),
                }),
            ),
            (
                bson!({
                    "version": "v1",
                    "cmd": "pub",
                    "topic": "/foo",
                    "payload": vec![100, 97, 116, 97]
                }),
                VersionedRpcCommand::V1(RpcCommand::Publish {
                    topic: "/foo".into(),
                    payload: b"data".to_vec(),
                }),
            ),
        ];

        for (command, expected) in commands {
            let rpc: VersionedRpcCommand = bson::from_bson(command).unwrap();
            assert_eq!(rpc, expected);
        }
    }

    #[test_case(r"$edgehub/rpc/foo", Some("foo"); "when word")]
    #[test_case(r"$edgehub/rpc/CA761232-ED42-11CE-BACD-00AA0057B223", Some("CA761232-ED42-11CE-BACD-00AA0057B223"); "when uuid")]
    #[test_case(r"$edgehub/rpc/ack/CA761232-ED42-11CE-BACD-00AA0057B223", None; "when ack")]
    #[test_case(r"$iothub/rpc/ack/CA761232-ED42-11CE-BACD-00AA0057B223", None; "when wrong topic")]
    #[test_case(r"$iothub/rpc/ack/some id", None; "when spaces")]
    fn it_captures_command_id(topic: &str, expected: Option<&str>) {
        assert_eq!(capture_command_id(topic).as_deref(), expected)
    }

    #[tokio::test]
    async fn it_handles_sub_command() {
        let upstream_pubs = MockPublishHandle::new();

        let mut upstream_subs = MockUpdateSubscriptionHandle::new();
        upstream_subs
            .expect_subscribe()
            .withf(|subscribe_to| subscribe_to.topic_filter == "/foo")
            .returning(|_| Ok(()));

        let mut downstream_pubs = MockPublishHandle::new();
        downstream_pubs
            .expect_publish()
            .once()
            .withf(|publication| publication.topic_name == "$edgehub/rpc/ack/1")
            .returning(|_| Ok(()));

        let mut handler = RpcHandler::new(upstream_subs, upstream_pubs, downstream_pubs);

        // send a command to subscribe to topic /foo
        let event = command("1", "sub", "/foo", None);
        let res = handler.handle(&event).await;
        assert_matches!(res, Ok(Handled::Fully));

        // emulate server response
        let event =
            Event::SubscriptionUpdates(vec![SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                topic_filter: "/foo".into(),
                qos: QoS::AtLeastOnce,
            })]);
        let res = handler.handle(&event).await;
        assert_matches!(res, Ok(Handled::Fully));
    }

    #[tokio::test]
    async fn it_send_nack_when_sub_to_upstream_rejected() {
        let upstream_pubs = MockPublishHandle::new();

        let mut upstream_subs = MockUpdateSubscriptionHandle::new();
        upstream_subs
            .expect_subscribe()
            .withf(|subscribe_to| subscribe_to.topic_filter == "/foo")
            .returning(|_| Ok(()));

        let mut downstream_pubs = MockPublishHandle::new();
        downstream_pubs
            .expect_publish()
            .once()
            .withf(|publication| publication.topic_name == "$edgehub/rpc/nack/1")
            .returning(|_| Ok(()));

        let mut handler = RpcHandler::new(upstream_subs, upstream_pubs, downstream_pubs);

        // send a command to subscribe to topic /foo
        let event = command("1", "sub", "/foo", None);
        let res = handler.handle(&event).await;
        assert_matches!(res, Ok(Handled::Fully));

        // emulate server response
        let event = Event::SubscriptionUpdates(vec![SubscriptionUpdateEvent::RejectedByServer(
            "/foo".into(),
        )]);
        let res = handler.handle(&event).await;
        assert_matches!(res, Ok(Handled::Fully));
    }

    #[tokio::test]
    async fn it_handles_unsub_command() {
        let upstream_pubs = MockPublishHandle::new();

        let mut upstream_subs = MockUpdateSubscriptionHandle::new();
        upstream_subs
            .expect_unsubscribe()
            .withf(|unsubscribe_from| unsubscribe_from == "/foo")
            .returning(|_| Ok(()));

        let mut downstream_pubs = MockPublishHandle::new();
        downstream_pubs
            .expect_publish()
            .once()
            .withf(|publication| publication.topic_name == "$edgehub/rpc/ack/1")
            .returning(|_| Ok(()));

        let mut handler = RpcHandler::new(upstream_subs, upstream_pubs, downstream_pubs);

        // send a command to unsubscribe from topic /foo
        let event = command("1", "unsub", "/foo", None);
        let res = handler.handle(&event).await;
        assert_matches!(res, Ok(Handled::Fully));

        // emulate server response
        let event =
            Event::SubscriptionUpdates(vec![SubscriptionUpdateEvent::Unsubscribe("/foo".into())]);
        let res = handler.handle(&event).await;
        assert_matches!(res, Ok(Handled::Fully));
    }

    #[tokio::test]
    async fn it_handles_pub_command() {
        let mut upstream_pubs = MockPublishHandle::new();
        upstream_pubs
            .expect_publish()
            .once()
            .withf(|publication| {
                publication.topic_name == "/foo" && publication.payload == Bytes::from("hello")
            })
            .returning(|_| Ok(()));

        let upstream_subs = MockUpdateSubscriptionHandle::new();

        let mut downstream_pubs = MockPublishHandle::new();
        downstream_pubs
            .expect_publish()
            .once()
            .withf(|publication| publication.topic_name == "$edgehub/rpc/ack/1")
            .returning(|_| Ok(()));

        let mut handler = RpcHandler::new(upstream_subs, upstream_pubs, downstream_pubs);

        let event = command("1", "pub", "/foo", Some(b"hello".to_vec()));
        let res = handler.handle(&event).await;

        assert_matches!(res, Ok(Handled::Fully));
    }

    fn command(id: &str, cmd: &str, topic: &str, payload: Option<Vec<u8>>) -> Event {
        let mut command = doc! {
            "version": "v1",
            "cmd": cmd,
            "topic": topic
        };
        if let Some(payload) = payload {
            command.insert(
                "payload",
                bson::Binary {
                    subtype: BinarySubtype::Generic,
                    bytes: payload,
                },
            );
        }

        let mut payload = Vec::new();
        command.to_writer(&mut payload).unwrap();

        Event::Publication(ReceivedPublication {
            topic_name: format!("$edgehub/rpc/{}", id),
            dup: false,
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: payload.into(),
        })
    }
}
