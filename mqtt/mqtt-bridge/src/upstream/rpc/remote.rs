use async_trait::async_trait;

use mqtt3::{Event, SubscriptionUpdateEvent};

use crate::{
    client::{EventHandler, Handled},
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
pub struct RemoteRpcHandler {
    subscriptions: RpcSubscriptions,
    local_pump: RpcPumpHandle,
}

impl RemoteRpcHandler {
    pub fn new(
        subscriptions: RpcSubscriptions,
        local_pump: PumpHandle<LocalUpstreamPumpEvent>,
    ) -> Self {
        Self {
            subscriptions,
            local_pump: RpcPumpHandle::new(local_pump),
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
            SubscriptionUpdateEvent::RejectedByServer(topic_filter) => {
                if let Some(command_id) = self.subscriptions.remove(topic_filter) {
                    let reason = format!("subscription rejected by server {}", topic_filter);
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
impl EventHandler for RemoteRpcHandler {
    type Error = RpcError;

    async fn handle(&mut self, mut event: Event) -> Result<Handled, Self::Error> {
        match &mut event {
            Event::Publication(_) => {
                // TODO implement incoming messages translation (C2D and M2M between EdgeHubs in nested edge scenario)
                Ok(Handled::Skipped(event))
            }
            Event::SubscriptionUpdates(subscriptions) => {
                // handle subscription updates
                let mut skipped = vec![];
                for subscription in subscriptions.drain(..) {
                    if !self.handle_subscription_update(&subscription).await? {
                        skipped.push(subscription);
                    }
                }

                if skipped.is_empty() {
                    Ok(Handled::Fully)
                } else {
                    let event = Event::SubscriptionUpdates(skipped);
                    Ok(Handled::Partially(event))
                }
            }
            _ => Ok(Handled::Skipped(event)),
        }
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
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;

    use mqtt3::{
        proto::{QoS, SubscribeTo},
        SubscriptionUpdateEvent,
    };

    use crate::pump::{self, PumpMessage};

    use super::*;

    #[tokio::test]
    async fn it_send_event_when_subscription_update_received() {
        let subscriptions = RpcSubscriptions::default();
        subscriptions.insert("1".into(), "/foo/subscribed");
        subscriptions.insert("2".into(), "/foo/rejected");
        subscriptions.insert("3".into(), "/foo/unsubscribed");

        let (local_pump, mut rx) = pump::channel();
        let mut handler = RemoteRpcHandler::new(subscriptions, local_pump);

        let event = Event::SubscriptionUpdates(vec![
            SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                topic_filter: "/foo/subscribed".into(),
                qos: QoS::AtLeastOnce,
            }),
            SubscriptionUpdateEvent::RejectedByServer("/foo/rejected".into()),
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
        subscriptions.insert("1".into(), "/foo/rpc");

        let (local_pump, mut rx) = pump::channel();
        let mut handler = RemoteRpcHandler::new(subscriptions, local_pump);

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
}
