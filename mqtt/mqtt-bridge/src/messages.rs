use std::convert::TryFrom;

use async_trait::async_trait;
use mqtt3::{proto::Publication, Event};
use mqtt_broker::TopicFilter;
use tracing::{debug, warn};

use crate::{
    bridge::BridgeError,
    client::{EventHandler, Handled},
    persist::{PublicationStore, StreamWakeableState},
    rpc::RpcHandler,
    settings::TopicRule,
};

#[derive(Clone)]
pub struct TopicMapper {
    topic_settings: TopicRule,
    topic_filter: TopicFilter,
}

impl TryFrom<TopicRule> for TopicMapper {
    type Error = BridgeError;

    fn try_from(topic: TopicRule) -> Result<Self, BridgeError> {
        let topic_filter = topic
            .pattern()
            .parse()
            .map_err(BridgeError::TopicFilterParse)?;

        Ok(Self {
            topic_settings: topic,
            topic_filter,
        })
    }
}

/// Handle events from client and saves them with the forward topic
pub struct MessageHandler<S> {
    topic_mappers: Vec<TopicMapper>,
    store: PublicationStore<S>,
}

impl<S> MessageHandler<S> {
    pub fn new(store: PublicationStore<S>, topic_mappers: Vec<TopicMapper>) -> Self {
        Self {
            topic_mappers,
            store: store,
        }
    }

    fn transform(&self, topic_name: &str) -> Option<String> {
        self.topic_mappers.iter().find_map(|mapper| {
            mapper
                .topic_settings
                .local()
                // maps if local does not have a value it uses the topic that was received,
                // else it checks that the received topic starts with local prefix and removes the local prefix
                .map_or(Some(topic_name), |local_prefix| {
                    let prefix = format!("{}/", local_prefix);
                    topic_name.strip_prefix(&prefix)
                })
                // match topic without local prefix with the topic filter pattern
                .filter(|stripped_topic| mapper.topic_filter.matches(stripped_topic))
                .map(|stripped_topic| {
                    if let Some(remote_prefix) = mapper.topic_settings.remote() {
                        format!("{}/{}", remote_prefix, stripped_topic)
                    } else {
                        stripped_topic.to_string()
                    }
                })
        })
    }
}

#[async_trait(?Send)]
impl<S> EventHandler for MessageHandler<S>
where
    S: StreamWakeableState,
{
    type Error = BridgeError;

    async fn handle(&mut self, event: &Event) -> Result<Handled, Self::Error> {
        if let Event::Publication(publication) = event {
            let forward_publication =
                self.transform(&publication.topic_name)
                    .map(|topic_name| Publication {
                        topic_name,
                        qos: publication.qos,
                        retain: publication.retain,
                        payload: publication.payload.clone(),
                    });

            if let Some(publication) = forward_publication {
                debug!("Save message to store");
                self.store.push(publication).map_err(BridgeError::Store)?;

                return Ok(Handled::Fully);
            } else {
                warn!("No topic matched");
            }
        }

        Ok(Handled::Skipped)
    }
}

pub struct UpstreamHandler<S> {
    messages: MessageHandler<S>,
    rpc: RpcHandler,
}

#[async_trait(?Send)]
impl<S> EventHandler for UpstreamHandler<S>
where
    S: StreamWakeableState,
{
    type Error = BridgeError;

    async fn handle(&mut self, event: &Event) -> Result<Handled, Self::Error> {
        // try to handle as RPC command first
        if self.rpc.handle(&event).await? == Handled::Fully {
            return Ok(Handled::Fully);
        }

        // handle as an event for regular message handler
        self.messages.handle(&event).await
    }
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use futures_util::{stream::StreamExt, TryStreamExt};
    use std::str::FromStr;

    use mqtt3::{
        proto::{Publication, QoS},
        Event, ReceivedPublication,
    };
    use mqtt_broker::TopicFilter;

    use super::{MessageHandler, TopicMapper};
    use crate::persist::PublicationStore;
    use crate::{client::EventHandler, settings::BridgeSettings};

    #[tokio::test]
    async fn message_handler_saves_message_with_local_and_forward_topic() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .forwards()
            .iter()
            .map(move |sub| TopicMapper {
                topic_settings: sub.clone(),
                topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = MessageHandler::new(store, topics);

        let pub1 = ReceivedPublication {
            topic_name: "local/floor/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        let expected = Publication {
            topic_name: "remote/floor/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler.handle(&Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_forward_topic() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .forwards()
            .iter()
            .map(move |sub| TopicMapper {
                topic_settings: sub.clone(),
                topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = MessageHandler::new(store, topics);

        let pub1 = ReceivedPublication {
            topic_name: "temp/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        let expected = Publication {
            topic_name: "floor/kitchen/temp/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler.handle(&Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_no_forward_mapping() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .forwards()
            .iter()
            .map(move |sub| TopicMapper {
                topic_settings: sub.clone(),
                topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = MessageHandler::new(store, topics);

        let pub1 = ReceivedPublication {
            topic_name: "pattern/p1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        let expected = Publication {
            topic_name: "pattern/p1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler.handle(&Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_no_topic_match() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .forwards()
            .iter()
            .map(move |sub| TopicMapper {
                topic_settings: sub.clone(),
                topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = MessageHandler::new(store, topics);

        let pub1 = ReceivedPublication {
            topic_name: "local/temp/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        handler.handle(&Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let mut interval = tokio::time::interval(std::time::Duration::from_secs(1));
        futures_util::future::select(interval.next(), loader.next()).await;
    }
}
