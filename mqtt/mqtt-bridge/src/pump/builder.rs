use std::convert::TryInto;

use tokio::sync::mpsc;

use crate::{
    bridge::BridgeError,
    client::{MqttClient, MqttClientConfig},
    messages::{MessageHandler, TopicMapper},
    persist::{PublicationStore, WakingMemoryStore},
    settings::TopicRule,
    upstream::{
        ConnectivityHandler, LocalRpcHandler, LocalUpstreamHandler, LocalUpstreamPumpEventHandler,
        RemoteRpcHandler, RemoteUpstreamHandler, RemoteUpstreamPumpEventHandler,
    },
};

use super::{MessagesProcessor, Pump, PumpHandle};

pub type PumpsResult = Result<
    (
        Pump<LocalUpstreamHandler<WakingMemoryStore>, LocalUpstreamPumpEventHandler>,
        Pump<RemoteUpstreamHandler<WakingMemoryStore>, RemoteUpstreamPumpEventHandler>,
    ),
    BridgeError,
>;

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
    pub fn with_local<F>(&mut self, mut apply: F) -> &mut Self
    where
        F: FnMut(&mut PumpBuilder),
    {
        apply(&mut self.local);
        self
    }

    pub fn with_remote<F>(&mut self, mut apply: F) -> &mut Self
    where
        F: FnMut(&mut PumpBuilder),
    {
        apply(&mut self.remote);
        self
    }

    pub fn with_store<F>(&mut self, store: F) -> &mut Self
    where
        F: Fn() -> PublicationStore<WakingMemoryStore> + 'static,
    {
        self.store = Box::new(store);
        self
    }

    pub fn build(&mut self) -> PumpsResult {
        let ingress_store = (self.store)();
        let ingress_loader = ingress_store.loader();

        let egress_store = (self.store)();
        let egress_loader = egress_store.loader();

        let (remote_messages_send, remote_messages_recv) = mpsc::channel(100);
        let (local_messages_send, local_messages_recv) = mpsc::channel(100);

        let (subscriptions, topic_filters) = make_topics(&self.local.rules)?;

        let rpc = LocalRpcHandler::new(PumpHandle::new(remote_messages_send.clone()));
        let messages = MessageHandler::new(ingress_store.clone(), topic_filters);
        let handler = LocalUpstreamHandler::new(messages, rpc);

        let config = self.local.client.take().expect("local client config");
        let client = MqttClient::tls(config, handler);
        let publish_handle = client
            .publish_handle()
            .map_err(BridgeError::PublishHandle)?;

        let handler = LocalUpstreamPumpEventHandler::new(publish_handle);
        let pump_handle = PumpHandle::new(local_messages_send.clone());
        let messages = MessagesProcessor::new(handler, local_messages_recv, pump_handle);

        let local_pump = Pump::new(
            local_messages_send.clone(),
            client,
            subscriptions,
            ingress_loader,
            egress_store.clone(),
            messages,
        )?;

        let (subscriptions, topic_filters) = make_topics(&self.remote.rules)?;

        let rpc = RemoteRpcHandler;
        let messages = MessageHandler::new(egress_store, topic_filters);
        let connectivity = ConnectivityHandler::new(PumpHandle::new(local_messages_send));
        let handler = RemoteUpstreamHandler::new(messages, rpc, connectivity);

        let config = self.remote.client.take().expect("remote client config");
        let client = MqttClient::tls(config, handler);

        let handler = RemoteUpstreamPumpEventHandler;
        let pump_handle = PumpHandle::new(remote_messages_send.clone());
        let messages = MessagesProcessor::new(handler, remote_messages_recv, pump_handle);

        let remote_pump = Pump::new(
            remote_messages_send,
            client,
            subscriptions,
            egress_loader,
            ingress_store,
            messages,
        )?;

        Ok((local_pump, remote_pump))
    }
}

#[derive(Default)]
pub struct PumpBuilder {
    client: Option<MqttClientConfig>,
    rules: Vec<TopicRule>,
}

impl PumpBuilder {
    pub fn with_rules(&mut self, rules: Vec<TopicRule>) -> &mut Self {
        self.rules = rules;
        self
    }

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
