use std::collections::HashMap;

use async_trait::async_trait;

use bson::{doc, Document};
use bytes::{buf::BufExt, Bytes};
use lazy_static::lazy_static;
use mqtt3::{
    proto::Publication, proto::QoS, proto::SubscribeTo, Event, PublishHandle,
    SubscriptionUpdateEvent, UpdateSubscriptionHandle,
};
use regex::Regex;
use serde::{Deserialize, Serialize};
use tracing::{error, warn};

use crate::client::EventHandler;

pub struct RpcHandler {
    upstream_subs: UpdateSubscriptionHandle,
    upstream_pubs: PublishHandle,
    downstream_pubs: PublishHandle,
    subscriptions: HashMap<String, String>,
}

impl RpcHandler {
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

    async fn handle_command(&mut self, command_id: String, command: RpcCommand) {
        match command {
            RpcCommand::Subscribe { topic_filter } => {
                self.handle_subscribe(command_id, topic_filter).await
            }
            RpcCommand::Unsubscribe { topic_filter } => {
                self.handle_unsubscribe(command_id, topic_filter).await
            }
            RpcCommand::Publish { topic, payload } => {
                self.handle_publish(command_id, topic, payload).await;
            }
        }
    }

    async fn handle_subscribe(&mut self, command_id: String, topic_filter: String) {
        let subscribe_to = SubscribeTo {
            topic_filter: topic_filter.clone(),
            qos: QoS::AtLeastOnce,
        };

        match self.upstream_subs.subscribe(subscribe_to).await {
            Ok(_) => {
                if let Some(existing) = self.subscriptions.insert(topic_filter, command_id) {
                    warn!("duplicating sub request found for {}", existing)
                }
            }
            Err(e) => {
                let reason = format!("unable to subscribe to upstream {}. {}", topic_filter, e);
                self.publish_nack(&command_id, reason).await;
            }
        }
    }

    async fn handle_unsubscribe(&mut self, command_id: String, topic_filter: String) {
        match self.upstream_subs.unsubscribe(topic_filter.clone()).await {
            Ok(_) => {
                if let Some(existing) = self.subscriptions.insert(topic_filter, command_id) {
                    warn!("duplicating unsub request found for {}", existing)
                }
            }
            Err(e) => {
                let reason = format!(
                    "unable to unsubscribe from upstream {}. {}",
                    topic_filter, e
                );
                self.publish_nack(&command_id, reason).await;
            }
        }
    }

    async fn handle_publish(&mut self, command_id: String, topic_name: String, payload: Vec<u8>) {
        let publication = Publication {
            topic_name: topic_name.clone(),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: payload.into(),
        };

        match self.upstream_pubs.publish(publication).await {
            Ok(_) => self.publish_ack(&command_id).await,
            Err(e) => {
                let reason = format!("unable to publish to upstream {}. {}", topic_name, e);
                self.publish_nack(&command_id, reason).await;
            }
        }
    }

    async fn handle_subcription_update(&mut self, subscription: &SubscriptionUpdateEvent) {
        match subscription {
            SubscriptionUpdateEvent::Subscribe(sub) => {
                if let Some(command_id) = self.subscriptions.remove(&sub.topic_filter) {
                    self.publish_ack(&command_id).await
                }
            }
            SubscriptionUpdateEvent::RejectedByServer(topic_filter) => {
                if let Some(command_id) = self.subscriptions.remove(topic_filter) {
                    let reason = format!("subscription rejected by server {}", topic_filter);
                    self.publish_nack(&command_id, reason).await
                }
            }
            SubscriptionUpdateEvent::Unsubscribe(topic_filter) => {
                if let Some(command_id) = self.subscriptions.remove(topic_filter) {
                    self.publish_ack(&command_id).await
                }
            }
        }
    }

    async fn publish_nack(&mut self, command_id: &str, reason: String) {
        let mut payload = Vec::new();
        let doc = doc! { "reason": reason };
        doc.to_writer(&mut payload);

        let publication = Publication {
            topic_name: format!("$edgehub/rpc/nack/{}", command_id),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: payload.into(),
        };

        if let Err(e) = self.downstream_pubs.publish(publication).await {
            error!("unable to send nack for {}. {}", command_id, e)
        }
    }

    async fn publish_ack(&mut self, command_id: &str) {
        let publication = Publication {
            topic_name: format!("$edgehub/rpc/ack/{}", command_id),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: Bytes::default(),
        };

        if let Err(e) = self.downstream_pubs.publish(publication).await {
            error!("unable to send ack for {}. {}", command_id, e)
        }
    }
}

#[async_trait]
impl EventHandler for RpcHandler {
    type Error = bson::de::Error;

    async fn handle(&mut self, event: &Event) -> Result<(), Self::Error> {
        match event {
            Event::Publication(publication) => {
                if let Some(command_id) = capture_command_id(&publication.topic_name) {
                    let doc = Document::from_reader(&mut publication.payload.clone().reader())?;
                    match bson::from_document(doc)? {
                        VersionedRpcCommand::V1(command) => {
                            self.handle_command(command_id, command).await
                        }
                    }
                }
            }
            Event::SubscriptionUpdates(subscriptions) => {
                for subscription in subscriptions {
                    self.handle_subcription_update(subscription).await
                }
            }
            _ => {}
        }

        Ok(())
    }
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
    use bson::bson;
    use test_case::test_case;

    use super::*;

    #[test]
    fn it_deserizes_from_bson() {
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
}
