use async_trait::async_trait;
use lazy_static::lazy_static;
use regex::RegexSet;

use mqtt3::{proto::Publication, Event, ReceivedPublication, SubscriptionUpdateEvent};
use tracing::debug;

use crate::{
    client::{Handled, MqttEventHandler},
    pump::PumpHandle,
    pump::PumpMessage,
    upstream::LocalUpstreamPumpEvent,
};

use super::{CommandId, RpcError, RpcSubscriptions};

/// An RPC handlers that responsible to connect to part of the bridge which
/// connects to upstream broker.
///
/// 1. It receives a subscription update, identifies those which related to
/// requested RPC commands and sends ACK or NACK to local pump as a
/// `PumpMessage` for each update.
///
/// 2. It receives a publication, identifies those which are for `IoTHub`
/// topics, translates topic and sends a special `PumpMessage` event to
/// local pump.
pub struct RemoteRpcMqttEventHandler {
    subscriptions: RpcSubscriptions,
    local_pump: RpcPumpHandle,
}

impl RemoteRpcMqttEventHandler {
    pub fn new(
        subscriptions: RpcSubscriptions,
        local_pump: PumpHandle<LocalUpstreamPumpEvent>,
    ) -> Self {
        Self {
            subscriptions,
            local_pump: RpcPumpHandle::new(local_pump),
        }
    }

    async fn handle_publication(
        &mut self,
        publication: &ReceivedPublication,
    ) -> Result<bool, RpcError> {
        if let Some(topic_name) = translate(&publication.topic_name) {
            debug!("forwarding incoming upstream publication to {}", topic_name);

            let publication = Publication {
                topic_name,
                qos: publication.qos,
                retain: publication.retain,
                payload: publication.payload.clone(),
            };
            self.local_pump.send_pub(publication).await?;
            Ok(true)
        } else {
            Ok(false)
        }
    }

    async fn handle_subscriptions(
        &mut self,
        subscriptions: impl IntoIterator<Item = SubscriptionUpdateEvent>,
    ) -> Result<Option<Vec<SubscriptionUpdateEvent>>, RpcError> {
        let mut skipped = vec![];
        for subscription in subscriptions {
            if !self.handle_subscription_update(&subscription).await? {
                skipped.push(subscription);
            }
        }

        if skipped.is_empty() {
            Ok(None)
        } else {
            Ok(Some(skipped))
        }
    }

    async fn handle_subscription_update(
        &mut self,
        subscription: &SubscriptionUpdateEvent,
    ) -> Result<bool, RpcError> {
        match subscription {
            SubscriptionUpdateEvent::Subscribe(sub) => {
                if let Some(command_id) = self.subscriptions.remove(&sub.topic_filter) {
                    self.local_pump.send_ack(command_id).await?;
                    return Ok(true);
                }
            }
            SubscriptionUpdateEvent::RejectedByServer(rejected_from) => {
                if let Some(command_id) = self.subscriptions.remove(&rejected_from.topic_filter) {
                    let reason = format!(
                        "subscription rejected by server {}",
                        rejected_from.topic_filter
                    );
                    self.local_pump.send_nack(command_id, reason).await?;
                    return Ok(true);
                }
            }
            SubscriptionUpdateEvent::Unsubscribe(topic_filter) => {
                if let Some(command_id) = self.subscriptions.remove(topic_filter) {
                    self.local_pump.send_ack(command_id).await?;
                    return Ok(true);
                }
            }
        }

        Ok(false)
    }
}

#[async_trait]
impl MqttEventHandler for RemoteRpcMqttEventHandler {
    type Error = RpcError;

    async fn handle(&mut self, event: Event) -> Result<Handled, Self::Error> {
        let event = match event {
            Event::Publication(publication) if self.handle_publication(&publication).await? => {
                return Ok(Handled::Fully);
            }
            Event::SubscriptionUpdates(subscriptions) => {
                let len = subscriptions.len();
                match self.handle_subscriptions(subscriptions).await? {
                    Some(skipped) if skipped.len() == len => {
                        let event = Event::SubscriptionUpdates(skipped);
                        return Ok(Handled::Skipped(event));
                    }
                    Some(skipped) => {
                        let event = Event::SubscriptionUpdates(skipped);
                        return Ok(Handled::Partially(event));
                    }
                    None => return Ok(Handled::Fully),
                };
            }
            event => event,
        };

        Ok(Handled::Skipped(event))
    }
}

fn translate(topic_name: &str) -> Option<String> {
    const DEVICE_OR_MODULE_ID: &str = r"(?P<device_id>[^/]+)(/(?P<module_id>[^/]+))?";

    lazy_static! {
        static ref UPSTREAM_TOPIC_PATTERNS: RegexSet = RegexSet::new(&[
            format!("\\$iothub/{}/twin/res/(?P<params>.*)", DEVICE_OR_MODULE_ID),
            format!(
                "\\$iothub/{}/twin/desired/(?P<params>.*)",
                DEVICE_OR_MODULE_ID
            ),
            format!(
                "\\$iothub/{}/methods/post/(?P<params>.*)",
                DEVICE_OR_MODULE_ID
            )
        ])
        .expect("upstream topic patterns");
    };

    if UPSTREAM_TOPIC_PATTERNS.is_match(topic_name) {
        Some(topic_name.replace("$iothub", "$downstream"))
    } else {
        None
    }
}

/// Convenient wrapper around `PumpHandle` for local pump that encapsulates
/// sending RPC command Ack, Nack or Publish.
pub struct RpcPumpHandle(PumpHandle<LocalUpstreamPumpEvent>);

impl RpcPumpHandle {
    pub fn new(handle: PumpHandle<LocalUpstreamPumpEvent>) -> Self {
        Self(handle)
    }

    pub async fn send_ack(&mut self, command_id: CommandId) -> Result<(), RpcError> {
        let event = LocalUpstreamPumpEvent::RpcAck(command_id.clone());
        self.0
            .send(PumpMessage::Event(event))
            .await
            .map_err(|e| RpcError::SendAck(command_id, e))
    }

    pub async fn send_nack(
        &mut self,
        command_id: CommandId,
        reason: String,
    ) -> Result<(), RpcError> {
        let event = LocalUpstreamPumpEvent::RpcNack(command_id.clone(), reason);
        self.0
            .send(PumpMessage::Event(event))
            .await
            .map_err(|e| RpcError::SendNack(command_id, e))
    }

    pub async fn send_pub(&mut self, publication: Publication) -> Result<(), RpcError> {
        let topic_name = publication.topic_name.clone();
        let event = LocalUpstreamPumpEvent::Publication(publication);
        self.0
            .send(PumpMessage::Event(event))
            .await
            .map_err(|e| RpcError::SendPublicationToLocalPump(topic_name, e))
    }
}

#[cfg(test)]
#[allow(clippy::semicolon_if_nothing_returned)]
mod tests {
    use matches::assert_matches;
    use test_case::test_case;

    use mqtt3::{
        proto::{QoS, SubscribeTo},
        SubscriptionUpdateEvent,
    };

    use crate::pump::{self, PumpMessage};

    use super::*;

    #[tokio::test]
    async fn it_send_event_when_subscription_update_received() {
        let subscriptions = RpcSubscriptions::default();
        subscriptions.insert("/foo/subscribed", "1".into());
        subscriptions.insert("/foo/rejected", "2".into());
        subscriptions.insert("/foo/unsubscribed", "3".into());

        let (local_pump, mut rx) = pump::channel();
        let mut handler = RemoteRpcMqttEventHandler::new(subscriptions, local_pump);

        let event = Event::SubscriptionUpdates(vec![
            SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                topic_filter: "/foo/subscribed".into(),
                qos: QoS::AtLeastOnce,
            }),
            SubscriptionUpdateEvent::RejectedByServer(SubscribeTo {
                qos: QoS::AtLeastOnce,
                topic_filter: "/foo/rejected".into(),
            }),
            SubscriptionUpdateEvent::Unsubscribe("/foo/unsubscribed".into()),
        ]);

        let res = handler.handle(event).await;
        assert_matches!(res, Ok(Handled::Fully));

        assert_matches!(
            rx.recv().await,
            Some(PumpMessage::Event(LocalUpstreamPumpEvent::RpcAck(id))) if id == "1".into()
        );
        assert_matches!(
            rx.recv().await,
            Some(PumpMessage::Event(LocalUpstreamPumpEvent::RpcNack(id, _))) if id == "2".into()
        );
        assert_matches!(
            rx.recv().await,
            Some(PumpMessage::Event(LocalUpstreamPumpEvent::RpcAck(id))) if id == "3".into()
        );
    }

    #[tokio::test]
    async fn it_returns_partially_handled_when_has_non_rpc() {
        let subscriptions = RpcSubscriptions::default();
        subscriptions.insert("/foo/rpc", "1".into());

        let (local_pump, mut rx) = pump::channel();
        let mut handler = RemoteRpcMqttEventHandler::new(subscriptions, local_pump);

        let event = Event::SubscriptionUpdates(vec![
            SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                topic_filter: "/foo/rpc".into(),
                qos: QoS::AtLeastOnce,
            }),
            SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                topic_filter: "/bar".into(),
                qos: QoS::AtLeastOnce,
            }),
        ]);

        let res = handler.handle(event).await;
        let expected =
            Event::SubscriptionUpdates(vec![SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                topic_filter: "/bar".into(),
                qos: QoS::AtLeastOnce,
            })]);
        assert_matches!(res, Ok(Handled::Partially(event)) if event == expected);

        assert_matches!(
            rx.recv().await,
            Some(PumpMessage::Event(LocalUpstreamPumpEvent::RpcAck(id))) if id == "1".into()
        );
    }

    #[tokio::test]
    async fn it_sends_publications_twin_response() {
        it_sends_publication_when_known_topic(
            "$iothub/device_1/module_a/twin/res/?$rid=1",
            "$downstream/device_1/module_a/twin/res/?$rid=1",
        )
        .await;
    }

    #[tokio::test]
    async fn it_sends_publications_twin_desired() {
        it_sends_publication_when_known_topic(
            "$iothub/device_1/module_a/twin/desired/?$rid=1",
            "$downstream/device_1/module_a/twin/desired/?$rid=1",
        )
        .await;
    }

    #[tokio::test]
    async fn it_sends_publications_direct_method() {
        it_sends_publication_when_known_topic(
            "$iothub/device_1/module_a/methods/post/?$rid=1",
            "$downstream/device_1/module_a/methods/post/?$rid=1",
        )
        .await;
    }

    async fn it_sends_publication_when_known_topic(topic_name: &str, translated_topic: &str) {
        let subscriptions = RpcSubscriptions::default();

        let (local_pump, mut rx) = pump::channel();
        let mut handler = RemoteRpcMqttEventHandler::new(subscriptions, local_pump);

        let event = Event::Publication(ReceivedPublication {
            topic_name: topic_name.into(),
            dup: false,
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: "hello".into(),
        });

        let res = handler.handle(event).await;
        assert_matches!(res, Ok(Handled::Fully));

        assert_matches!(
            rx.recv().await,
            Some(PumpMessage::Event(LocalUpstreamPumpEvent::Publication(publication))) if publication.topic_name == translated_topic
        );
    }

    #[tokio::test]
    async fn it_skips_publication_when_unknown_topic() {
        let subscriptions = RpcSubscriptions::default();

        let (local_pump, _rx) = pump::channel();
        let mut handler = RemoteRpcMqttEventHandler::new(subscriptions, local_pump);

        let event = Event::Publication(ReceivedPublication {
            topic_name: "/foo".into(),
            dup: false,
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: "hello".into(),
        });

        let res = handler.handle(event).await;
        assert_matches!(res, Ok(Handled::Skipped(_)));
    }

    #[test_case("$iothub/device_1/module_a/twin/res/?$rid=1", Some("$downstream/device_1/module_a/twin/res/?$rid=1"); "twin module")]
    #[test_case("$iothub/device_1/twin/res/?$rid=1", Some("$downstream/device_1/twin/res/?$rid=1"); "twin device")]
    #[test_case("$iothub/device_1/module_a/twin/desired/?$rid=1", Some("$downstream/device_1/module_a/twin/desired/?$rid=1"); "desired twin module")]
    #[test_case("$iothub/device_1/twin/desired/?$rid=1", Some("$downstream/device_1/twin/desired/?$rid=1"); "desired twin device")]
    #[test_case("$iothub/device_1/module_a/methods/post/?$rid=1", Some("$downstream/device_1/module_a/methods/post/?$rid=1"); "direct method module")]
    #[test_case("$iothub/device_1/methods/post/?$rid=1", Some("$downstream/device_1/methods/post/?$rid=1"); "direct method device")]
    #[test_case("$edgehub/device_1/module_a/twin/res/?$rid=1", None; "wrong prefix")]
    fn it_translates_upstream_topic(topic_name: &str, expected: Option<&str>) {
        assert_eq!(translate(topic_name).as_deref(), expected);
    }
}
