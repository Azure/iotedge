use std::{collections::HashMap, convert::TryInto};

use tokio::sync::mpsc;
use tokio_stream::wrappers::UnboundedReceiverStream;

use crate::{
    bridge::BridgeError,
    client::{MqttClient, MqttClientConfig, MqttClientExt},
    messages::{self, StoreMqttEventHandler, TopicMapper},
    persist::{PersistResult, PublicationStore, StreamWakeableState},
    settings::TopicRule,
    upstream::{
        ConnectivityMqttEventHandler, LocalRpcMqttEventHandler, LocalUpstreamMqttEventHandler,
        LocalUpstreamPumpEventHandler, RemoteRpcMqttEventHandler, RemoteUpstreamMqttEventHandler,
        RemoteUpstreamPumpEventHandler, RpcSubscriptions,
    },
};

use super::{MessagesProcessor, Pump, PumpHandle, TopicMapperUpdates};

pub type PumpPair<S> = (
    Pump<S, LocalUpstreamMqttEventHandler<S>, LocalUpstreamPumpEventHandler>,
    Pump<S, RemoteUpstreamMqttEventHandler<S>, RemoteUpstreamPumpEventHandler>,
);

type StoreCreateFn<S> = dyn Fn(&str) -> PersistResult<PublicationStore<S>>;
type BoxedStorageCreatedFn<S> = Box<StoreCreateFn<S>>;

/// Constructs a pair of bridge pumps: local and remote.
///
/// Local pump connects to a local broker, subscribes to topics to receive
/// messages from local broker and put it in the store of the remote pump.
/// Also reads messages from a local store and publishes them to local broker.
///
/// Remote pump connects to a remote broker, subscribes to topics to receive
/// messages from remote broker and put it in the store of the local pump.
/// Also reads messages from a remote store and publishes them to local broker.
pub struct Builder<S> {
    local: PumpBuilder,
    remote: PumpBuilder,
    store: Option<BoxedStorageCreatedFn<S>>,
}

impl<S> Default for Builder<S> {
    fn default() -> Self {
        Self {
            local: PumpBuilder::default(),
            remote: PumpBuilder::default(),
            store: None,
        }
    }
}

impl<S> Builder<S>
where
    S: StreamWakeableState + Send,
{
    /// Apples parameters to create local pump.
    #[must_use]
    pub fn with_local<F>(mut self, mut apply: F) -> Self
    where
        F: FnMut(&mut PumpBuilder),
    {
        apply(&mut self.local);
        self
    }

    /// Applies parameters to create remote pump.
    #[must_use]
    pub fn with_remote<F>(mut self, mut apply: F) -> Self
    where
        F: FnMut(&mut PumpBuilder),
    {
        apply(&mut self.remote);
        self
    }

    /// Setups a factory to create publication store.
    pub fn with_store<F, S1>(self, store: F) -> Builder<S1>
    where
        F: Fn(&str) -> PersistResult<PublicationStore<S1>> + 'static,
    {
        Builder {
            local: self.local,
            remote: self.remote,
            store: Some(Box::new(store)),
        }
    }

    /// Creates a pair of local and remote pump.
    pub fn build(&mut self) -> Result<PumpPair<S>, BridgeError> {
        let store = self.store.as_ref().ok_or(BridgeError::UnsetStorage)?;

        let remote_store = (store)("remote")?;
        let local_store = (store)("local")?;

        let (remote_messages_send, remote_messages_recv) = mpsc::channel(100);
        let (local_messages_send, local_messages_recv) = mpsc::channel(100);

        // prepare local pump
        let topic_filters = make_topics(&self.local.rules)?;
        let local_topic_mappers_updates = TopicMapperUpdates::new(topic_filters);

        let rpc = LocalRpcMqttEventHandler::new(PumpHandle::new(remote_messages_send.clone()));
        let messages =
            StoreMqttEventHandler::new(remote_store.clone(), local_topic_mappers_updates.clone());

        let handler = LocalUpstreamMqttEventHandler::new(messages, rpc);

        let config = self.local.client.take().expect("local client config");
        let client = MqttClient::tcp(config, handler).map_err(BridgeError::ValidationError)?;
        let local_pub_handle = client
            .publish_handle()
            .map_err(BridgeError::PublishHandle)?;
        let subscription_handle = client
            .update_subscription_handle()
            .map_err(BridgeError::UpdateSubscriptionHandle)?;

        let handler = LocalUpstreamPumpEventHandler::new(local_pub_handle);
        let pump_handle = PumpHandle::new(local_messages_send.clone());
        let messages = MessagesProcessor::new(
            handler,
            local_messages_recv,
            pump_handle,
            subscription_handle,
            local_topic_mappers_updates,
        );

        let local_pump = Pump::new(
            local_messages_send.clone(),
            client,
            local_store.clone(),
            messages,
        )?;

        // prepare remote pump
        let topic_filters = make_topics(&self.remote.rules)?;
        let remote_topic_mappers_updates = TopicMapperUpdates::new(topic_filters);

        let (retry_send, retry_recv) = mpsc::unbounded_channel();

        let rpc_subscriptions = RpcSubscriptions::default();
        let rpc = RemoteRpcMqttEventHandler::new(rpc_subscriptions.clone(), local_pump.handle());
        let mut messages =
            StoreMqttEventHandler::new(local_store, remote_topic_mappers_updates.clone());
        messages.set_retry_sub_sender(retry_send);

        let connectivity = ConnectivityMqttEventHandler::new(PumpHandle::new(local_messages_send));
        let handler = RemoteUpstreamMqttEventHandler::new(messages, rpc, connectivity);

        let config = self.remote.client.take().expect("remote client config");
        let client = MqttClient::tls(config, handler).map_err(BridgeError::ValidationError)?;
        let remote_pub_handle = client
            .publish_handle()
            .map_err(BridgeError::PublishHandle)?;
        let remote_sub_handle = client
            .update_subscription_handle()
            .map_err(BridgeError::UpdateSubscriptionHandle)?;

        let retry_sub_handle = client
            .update_subscription_handle()
            .map_err(BridgeError::UpdateSubscriptionHandle)?;

        tokio::spawn(messages::retry_subscriptions(
            UnboundedReceiverStream::new(retry_recv),
            remote_topic_mappers_updates.clone(),
            retry_sub_handle,
        ));

        let handler = RemoteUpstreamPumpEventHandler::new(
            remote_sub_handle,
            remote_pub_handle,
            local_pump.handle(),
            rpc_subscriptions,
        );
        let pump_handle = PumpHandle::new(remote_messages_send.clone());

        let remote_sub_handle = client
            .update_subscription_handle()
            .map_err(BridgeError::UpdateSubscriptionHandle)?;
        let messages = MessagesProcessor::new(
            handler,
            remote_messages_recv,
            pump_handle,
            remote_sub_handle,
            remote_topic_mappers_updates,
        );

        let remote_pump = Pump::new(remote_messages_send, client, remote_store, messages)?;

        Ok((local_pump, remote_pump))
    }
}

/// Collects parameters to construct `Pump`.
#[derive(Default)]
pub struct PumpBuilder {
    client: Option<MqttClientConfig>,
    rules: Vec<TopicRule>,
}

impl PumpBuilder {
    /// Applies default topic translation rules.
    pub fn with_rules(&mut self, rules: Vec<TopicRule>) -> &mut Self {
        self.rules = rules;
        self
    }

    /// Applies MQTT client settings.
    pub fn with_config(&mut self, config: MqttClientConfig) -> &mut Self {
        self.client = Some(config);
        self
    }
}

fn make_topics(rules: &[TopicRule]) -> Result<HashMap<String, TopicMapper>, BridgeError> {
    let topic_filters: Vec<TopicMapper> = rules
        .iter()
        .map(|topic| topic.clone().try_into())
        .collect::<Result<Vec<_>, _>>()?;

    let topic_filters = topic_filters
        .iter()
        .map(|topic| (topic.subscribe_to(), topic.clone()))
        .collect::<HashMap<_, _>>();

    Ok(topic_filters)
}
