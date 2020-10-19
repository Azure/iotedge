use std::convert::TryInto;

use tokio::sync::mpsc;

use crate::{
    bridge::BridgeError,
    client::{MqttClient, MqttClientConfig},
    messages::{MessageHandler, TopicMapper},
    persist::{PublicationStore, WakingMemoryStore},
    settings::TopicRule,
    upstream::{
        LocalRpcHandler, LocalUpstreamHandler, LocalUpstreamPumpEventHandler, RemoteRpcHandler,
        RemoteUpstreamHandler, RemoteUpstreamPumpEventHandler,
    },
};

use super::{MessagesProcessor, Pump, PumpHandle};

pub type PumpPair = (
    Pump<LocalUpstreamHandler<WakingMemoryStore>, LocalUpstreamPumpEventHandler>,
    Pump<RemoteUpstreamHandler<WakingMemoryStore>, RemoteUpstreamPumpEventHandler>,
);

/// Constructs a pair of bridge pumps: local and remote.
///
/// Local pump connects to a local broker, subscribes to topics to receive
/// messages from local broker and put it in the store of the remote pump.
/// Also reads messages from a local store and publishes them to local broker.
///
/// Remote pump connects to a remote broker, subscribes to topics to receive
/// messages from remote broker and put it in the store of the local pump.
/// Also reads messages from a remote store and publishes them to local broker.
pub struct Builder {
    local: PumpBuilder,
    remote: PumpBuilder,
    store: Box<dyn Fn() -> PublicationStore<WakingMemoryStore>>,
}

impl Default for Builder {
    fn default() -> Self {
        Self {
            local: PumpBuilder::default(),
            remote: PumpBuilder::default(),
            store: Box::new(|| PublicationStore::new_memory(0)),
        }
    }
}

impl Builder {
    /// Apples parameters to create local pump.
    pub fn with_local<F>(&mut self, mut apply: F) -> &mut Self
    where
        F: FnMut(&mut PumpBuilder),
    {
        apply(&mut self.local);
        self
    }

    /// Applies parameters to create remote pump.
    pub fn with_remote<F>(&mut self, mut apply: F) -> &mut Self
    where
        F: FnMut(&mut PumpBuilder),
    {
        apply(&mut self.remote);
        self
    }

    /// Setups a factory to create publication store.
    pub fn with_store<F>(&mut self, store: F) -> &mut Self
    where
        F: Fn() -> PublicationStore<WakingMemoryStore> + 'static,
    {
        self.store = Box::new(store);
        self
    }

    /// Creates a pair of local and remote pump.
    pub fn build(&mut self) -> Result<PumpPair, BridgeError> {
        let remote_store = (self.store)();
        let local_store = (self.store)();

        let (remote_messages_send, remote_messages_recv) = mpsc::channel(100);
        let (local_messages_send, local_messages_recv) = mpsc::channel(100);

        let (subscriptions, topic_filters) = make_topics(&self.local.rules)?;

        let rpc = LocalRpcHandler::new(PumpHandle::new(remote_messages_send.clone()));
        let messages = MessageHandler::new(remote_store.clone(), topic_filters);
        let handler = LocalUpstreamHandler::new(messages, rpc);

        let config = self.local.client.take().expect("local client config");
        let client = MqttClient::tls(config, handler);

        let handler = LocalUpstreamPumpEventHandler;
        let pump_handle = PumpHandle::new(local_messages_send.clone());
        let messages = MessagesProcessor::new(handler, local_messages_recv, pump_handle);

        let local_pump = Pump::new(
            local_messages_send,
            client,
            subscriptions,
            local_store.clone(),
            messages,
        )?;

        let (subscriptions, topic_filters) = make_topics(&self.remote.rules)?;

        let rpc = RemoteRpcHandler;
        let messages = MessageHandler::new(local_store, topic_filters);
        let handler = RemoteUpstreamHandler::new(messages, rpc);

        let config = self.local.client.take().expect("local client config");
        let client = MqttClient::tls(config, handler);

        let handler = RemoteUpstreamPumpEventHandler;
        let pump_handle = PumpHandle::new(remote_messages_send.clone());
        let messages = MessagesProcessor::new(handler, remote_messages_recv, pump_handle);

        let remote_pump = Pump::new(
            remote_messages_send,
            client,
            subscriptions,
            remote_store,
            messages,
        )?;

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

fn make_topics(rules: &[TopicRule]) -> Result<(Vec<String>, Vec<TopicMapper>), BridgeError> {
    let (subscriptions, topic_rules): (Vec<_>, Vec<_>) = rules.iter().map(format_key_value).unzip();
    let topic_filters = topic_rules
        .into_iter()
        .map(|topic| topic.try_into())
        .collect::<Result<Vec<_>, _>>()?;

    Ok((subscriptions, topic_filters))
}

fn format_key_value(topic: &TopicRule) -> (String, TopicRule) {
    let key = if let Some(local) = topic.in_prefix() {
        format!("{}/{}", local, topic.topic())
    } else {
        topic.topic().into()
    };
    (key, topic.clone())
}
