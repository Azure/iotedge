use async_trait::async_trait;
use mqtt3::proto::{Publication, QoS, SubscribeTo};
use tracing::{error, warn};

use crate::{
    pump::{PumpHandle, PumpMessageHandler},
    upstream::{
        CommandId, LocalUpstreamPumpEvent, RpcCommand, RpcError, RpcPumpHandle, RpcSubscriptions,
    },
};

// Import and use mocks when run tests, real implementation when otherwise
#[cfg(test)]
use crate::client::{
    MockPublishHandle as PublishHandle, MockUpdateSubscriptionHandle as UpdateSubscriptionHandle,
};
#[cfg(not(test))]
use crate::client::{PublishHandle, UpdateSubscriptionHandle};

/// Pump control event for a remote upstream bridge pump.
#[derive(Debug, PartialEq)]
pub enum RemoteUpstreamPumpEvent {
    RpcCommand(CommandId, RpcCommand),
}

/// Handles control event received by a remote upstream bridge pump.
///
/// It handles following events:
/// * RPC command - emitted when `EdgeHub` requested RPC command to be executed
/// against remote broker.
pub struct RemoteUpstreamPumpEventHandler {
    remote_sub_handle: UpdateSubscriptionHandle,
    remote_pub_handle: PublishHandle,
    local_pump: RpcPumpHandle,
    subscriptions: RpcSubscriptions,
}

impl RemoteUpstreamPumpEventHandler {
    pub fn new(
        remote_sub_handle: UpdateSubscriptionHandle,
        remote_pub_handle: PublishHandle,
        local_pump_handle: PumpHandle<LocalUpstreamPumpEvent>,
        subscriptions: RpcSubscriptions,
    ) -> Self {
        Self {
            remote_sub_handle,
            remote_pub_handle,
            local_pump: RpcPumpHandle::new(local_pump_handle),
            subscriptions,
        }
    }

    async fn handle_command(
        &mut self,
        command_id: CommandId,
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
        command_id: CommandId,
        topic_filter: String,
    ) -> Result<(), RpcError> {
        let subscribe_to = SubscribeTo {
            topic_filter: topic_filter.clone(),
            qos: QoS::AtLeastOnce,
        };

        match self.remote_sub_handle.subscribe(subscribe_to).await {
            Ok(_) => {
                if let Some(existing) = self.subscriptions.insert(&topic_filter, command_id) {
                    warn!(
                        command_id = ?existing,
                        "duplicating sub request found for topic {}", topic_filter
                    );
                }
            }
            Err(e) => {
                let reason = format!("unable to subscribe to upstream {}. {}", topic_filter, e);
                self.local_pump.send_nack(command_id, reason).await?;
            }
        }

        Ok(())
    }

    async fn handle_unsubscribe(
        &mut self,
        command_id: CommandId,
        topic_filter: String,
    ) -> Result<(), RpcError> {
        match self
            .remote_sub_handle
            .unsubscribe(topic_filter.clone())
            .await
        {
            Ok(_) => {
                if let Some(existing) = self.subscriptions.insert(&topic_filter, command_id) {
                    warn!(
                        commands = ?existing,
                        "duplicating unsub request found for topic {}", topic_filter
                    );
                }
            }
            Err(e) => {
                let reason = format!(
                    "unable to unsubscribe from upstream {}. {}",
                    topic_filter, e
                );
                self.local_pump.send_nack(command_id, reason).await?;
            }
        }

        Ok(())
    }

    async fn handle_publish(
        &mut self,
        command_id: CommandId,
        topic_name: String,
        payload: Vec<u8>,
    ) -> Result<(), RpcError> {
        let publication = Publication {
            topic_name: topic_name.clone(),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: payload.into(),
        };

        match self.remote_pub_handle.publish(publication).await {
            Ok(_) => self.local_pump.send_ack(command_id).await,
            Err(e) => {
                let reason = format!("unable to publish to upstream {}. {}", topic_name, e);
                self.local_pump.send_nack(command_id, reason).await
            }
        }
    }
}

#[async_trait]
impl PumpMessageHandler for RemoteUpstreamPumpEventHandler {
    type Message = RemoteUpstreamPumpEvent;

    async fn handle(&mut self, message: Self::Message) {
        match message {
            RemoteUpstreamPumpEvent::RpcCommand(command_id, command) => {
                let cmd_string = command.to_string();
                if let Err(e) = self.handle_command(command_id.clone(), command).await {
                    error!(
                        "unable to handle rpc command {} {}. {}",
                        command_id, cmd_string, e
                    );
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use tokio::sync::mpsc::error::TryRecvError;

    use bytes::Bytes;
    use matches::assert_matches;

    use crate::{
        client::{MockPublishHandle, MockUpdateSubscriptionHandle},
        pump::{self, PumpMessage},
    };

    use super::*;

    #[tokio::test]
    async fn it_handles_sub_command() {
        let remote_pub_handle = MockPublishHandle::new();

        let mut remote_sub_handle = MockUpdateSubscriptionHandle::new();
        remote_sub_handle
            .expect_subscribe()
            .withf(|subscribe_to| subscribe_to.topic_filter == "/foo")
            .returning(|_| Ok(()));

        let (local_pump, mut rx) = pump::channel();

        let rpc_subscriptions = RpcSubscriptions::default();
        let mut handler = RemoteUpstreamPumpEventHandler::new(
            remote_sub_handle,
            remote_pub_handle,
            local_pump,
            rpc_subscriptions.clone(),
        );

        // handle a command to subscribe to topic /foo
        let command = RpcCommand::Subscribe {
            topic_filter: "/foo".into(),
        };
        let event = RemoteUpstreamPumpEvent::RpcCommand("1".into(), command);
        handler.handle(event).await;

        // check no message which was sent to local pump
        assert_matches!(rx.try_recv(), Err(TryRecvError::Empty));

        // check subscriptions has requested topic
        assert_matches!(rpc_subscriptions.remove("/foo"), Some(id) if id == "1".into());
    }

    #[tokio::test]
    async fn it_handles_unsub_command() {
        let remote_pub_handle = MockPublishHandle::new();

        let mut remote_sub_handle = MockUpdateSubscriptionHandle::new();
        remote_sub_handle
            .expect_unsubscribe()
            .withf(|subscribe_from| subscribe_from == "/foo")
            .returning(|_| Ok(()));

        let (local_pump, mut rx) = pump::channel();

        let rpc_subscriptions = RpcSubscriptions::default();
        let mut handler = RemoteUpstreamPumpEventHandler::new(
            remote_sub_handle,
            remote_pub_handle,
            local_pump,
            rpc_subscriptions.clone(),
        );

        // handle a command to unsubscribe from topic /foo
        let command = RpcCommand::Unsubscribe {
            topic_filter: "/foo".into(),
        };
        let event = RemoteUpstreamPumpEvent::RpcCommand("1".into(), command);
        handler.handle(event).await;

        // check no message which was sent to local pump
        assert_matches!(rx.try_recv(), Err(TryRecvError::Empty));

        // check subscriptions has requested topic
        assert_matches!(rpc_subscriptions.remove("/foo"), Some(id) if id == "1".into());
    }

    #[tokio::test]
    async fn it_handles_pub_command() {
        let mut remote_pub_handle = MockPublishHandle::new();
        remote_pub_handle
            .expect_publish()
            .once()
            .withf(|publication| {
                publication.topic_name == "/foo" && publication.payload == Bytes::from("hello")
            })
            .returning(|_| Ok(()));

        let remote_sub_handle = MockUpdateSubscriptionHandle::new();

        let (local_pump, mut rx) = pump::channel();

        let rpc_subscriptions = RpcSubscriptions::default();
        let mut handler = RemoteUpstreamPumpEventHandler::new(
            remote_sub_handle,
            remote_pub_handle,
            local_pump,
            rpc_subscriptions,
        );

        // handle a command to publish on topic /foo
        let command = RpcCommand::Publish {
            topic: "/foo".into(),
            payload: b"hello".to_vec(),
        };
        let event = RemoteUpstreamPumpEvent::RpcCommand("1".into(), command);
        handler.handle(event).await;

        // check message which was sent to local pump
        assert_matches!(
            rx.recv().await,
            Some(PumpMessage::Event(LocalUpstreamPumpEvent::RpcAck(id))) if id == "1".into()
        );
    }
}
