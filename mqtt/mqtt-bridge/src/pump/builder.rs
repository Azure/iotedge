// #![allow(dead_code, unused_imports, unused_variables)]
use std::convert::TryInto;

use tokio::sync::mpsc;

use crate::{
    bridge::BridgeError,
    client::{MqttClient, MqttClientConfig},
    messages::{MessageHandler, TopicMapper},
    persist::{PublicationStore, WakingMemoryStore},
    settings::TopicRule,
    upstream::{LocalRpcHandler, LocalUpstreamHandler, RemoteRpcHandler, RemoteUpstreamHandler},
};

use super::{Pump, PumpHandle};

pub type PumpsResult = Result<
    (
        Pump<LocalUpstreamHandler<WakingMemoryStore>>,
        Pump<RemoteUpstreamHandler<WakingMemoryStore>>,
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
        let local_pump = Pump::new(
            local_messages_send,
            local_messages_recv,
            client,
            subscriptions,
            ingress_loader,
            egress_store.clone(),
        )?;

        let (subscriptions, topic_filters) = make_topics(&self.remote.rules)?;

        let rpc = RemoteRpcHandler;
        let messages = MessageHandler::new(egress_store, topic_filters);
        let handler = RemoteUpstreamHandler::new(messages, rpc);

        let config = self.local.client.take().expect("local client config");
        let client = MqttClient::tls(config, handler);
        let remote_pump = Pump::new(
            remote_messages_send,
            remote_messages_recv,
            client,
            subscriptions,
            egress_loader,
            ingress_store,
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
    pub fn with_rules(&mut self, rules: &[TopicRule]) -> &mut Self {
        self.rules = rules.to_vec();
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
    let key = if let Some(local) = topic.local() {
        format!("{}/{}", local, topic.pattern())
    } else {
        topic.pattern().into()
    };
    (key, topic.clone())
}
