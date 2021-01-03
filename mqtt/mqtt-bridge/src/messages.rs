use std::{collections::HashMap, convert::TryFrom};

use async_trait::async_trait;
use mqtt3::{proto::Publication, Event, SubscriptionUpdateEvent};
use mqtt_broker::TopicFilter;
use tracing::{debug, warn};

use crate::{
    bridge::BridgeError,
    client::{Handled, MqttEventHandler},
    persist::{PublicationStore, StreamWakeableState},
    pump::TopicMapperUpdates,
    settings::TopicRule,
};

#[derive(Default, Clone)]
pub struct TopicMapper {
    topic_settings: TopicRule,
    topic_filter: TopicFilter,
}

impl TopicMapper {
    pub fn subscribe_to(&self) -> String {
        self.topic_settings.subscribe_to()
    }
}

impl TryFrom<TopicRule> for TopicMapper {
    type Error = BridgeError;

    fn try_from(topic: TopicRule) -> Result<Self, BridgeError> {
        let topic_filter = topic
            .topic()
            .parse()
            .map_err(BridgeError::TopicFilterParse)?;

        Ok(Self {
            topic_settings: topic,
            topic_filter,
        })
    }
}

/// Handle events from client and saves them with the forward topic
pub struct StoreMqttEventHandler<S> {
    topic_mappers: HashMap<String, TopicMapper>,
    topic_mappers_updates: TopicMapperUpdates,
    store: PublicationStore<S>,
}

impl<S> StoreMqttEventHandler<S> {
    pub fn new(store: PublicationStore<S>, topic_mappers_updates: TopicMapperUpdates) -> Self {
        Self {
            topic_mappers: HashMap::new(),
            topic_mappers_updates,
            store,
        }
    }

    fn transform(&self, topic_name: &str) -> Option<String> {
        self.topic_mappers.values().find_map(|mapper| {
            mapper
                .topic_settings
                .in_prefix()
                // maps if local does not have a value it uses the topic that was received,
                // else it checks that the received topic starts with local prefix and removes the local prefix
                .map_or(Some(topic_name), |local_prefix| {
                    topic_name.strip_prefix(format!("{}/", local_prefix).as_str())
                })
                // match topic without local prefix with the topic filter pattern
                .filter(|stripped_topic| mapper.topic_filter.matches(stripped_topic))
                .map(|stripped_topic| match mapper.topic_settings.out_prefix() {
                    Some(remote_prefix) => format!("{}/{}", remote_prefix, stripped_topic),
                    None => stripped_topic.to_string(),
                })
        })
    }

    fn update_subscribed(&mut self, sub: &str) {
        if let Some(mapper) = self.topic_mappers_updates.get(sub) {
            self.topic_mappers.insert(sub.to_owned(), mapper);
        } else {
            warn!("unexpected subscription ack for {}", sub);
        };
    }

    fn update_unsubscribed(&mut self, sub: &str) {
        if self.topic_mappers.remove(sub).is_none() {
            warn!("unexpected subscription/rejected ack for {}", sub);
        };
    }
}

#[async_trait]
impl<S> MqttEventHandler for StoreMqttEventHandler<S>
where
    S: StreamWakeableState + Send,
{
    type Error = BridgeError;

    fn subscriptions(&self) -> Vec<String> {
        self.topic_mappers_updates.subscriptions()
    }

    async fn handle(&mut self, event: Event) -> Result<Handled, Self::Error> {
        match &event {
            Event::Publication(publication) => {
                let forward_publication =
                    self.transform(&publication.topic_name)
                        .map(|topic_name| Publication {
                            topic_name,
                            qos: publication.qos,
                            retain: publication.retain,
                            payload: publication.payload.clone(),
                        });

                if let Some(publication) = forward_publication {
                    debug!("saving message to store");
                    self.store.push(publication).map_err(BridgeError::Store)?;

                    return Ok(Handled::Fully);
                } else {
                    warn!("no topic matched");
                }
            }
            Event::SubscriptionUpdates(sub_updates) => {
                for update in sub_updates {
                    match update {
                        SubscriptionUpdateEvent::Subscribe(subscribe_to) => {
                            debug!("received subscribe: {:?}", subscribe_to);
                            self.update_subscribed(&subscribe_to.topic_filter);
                        }
                        SubscriptionUpdateEvent::Unsubscribe(unsubcribed_from) => {
                            debug!("received unsubscribe: {}", unsubcribed_from);
                            self.update_unsubscribed(&unsubcribed_from);
                        }
                        SubscriptionUpdateEvent::RejectedByServer(rejected) => {
                            debug!("received subscription rejected: {}", rejected);
                            self.update_unsubscribed(&rejected);
                        }
                    }
                }

                return Ok(Handled::Fully);
            }
            Event::NewConnection { reset_session: _ } | Event::Disconnected(_) => {}
        }

        Ok(Handled::Skipped(event))
    }
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use futures_util::{
        future::{self, Either},
        stream::StreamExt,
        TryStreamExt,
    };
    use std::{collections::HashMap, str::FromStr};

    use mqtt3::{
        proto::{Publication, QoS, SubscribeTo},
        Event, ReceivedPublication, SubscriptionUpdateEvent,
    };
    use mqtt_broker::TopicFilter;

    use super::{StoreMqttEventHandler, TopicMapper};

    use crate::{
        client::MqttEventHandler, persist::PublicationStore, pump::TopicMapperUpdates,
        settings::BridgeSettings,
    };

    #[tokio::test]
    async fn message_handler_updates_topic() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: HashMap<String, TopicMapper> = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    },
                )
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "local/floor/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        let _topic_mapper = handler.topic_mappers.get("local/floor/#").unwrap();
    }

    #[tokio::test]
    async fn message_handler_updates_topic_without_pending_update() {
        let batch_size: usize = 5;

        let topics = HashMap::new();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "local/floor/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        assert_eq!(handler.topic_mappers.get("local/floor/#").is_none(), true);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_local_and_forward_topic() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    },
                )
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

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

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "local/floor/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();
        handler.handle(Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_empty_local_and_forward_topic() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    },
                )
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "floor2/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        let expected = Publication {
            topic_name: "floor2/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "floor2/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        handler.handle(Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_forward_topic() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    },
                )
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

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

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "temp/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();
        handler.handle(Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_no_forward_mapping() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    },
                )
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

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

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "pattern/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        handler.handle(Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_no_topic_match() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    },
                )
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "local/temp/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "local/temp/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();
        handler.handle(Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let mut interval = tokio::time::interval(std::time::Duration::from_secs(1));
        if let Either::Right(_) = future::select(interval.next(), loader.next()).await {
            panic!("Should not reach here");
        }
    }

    #[tokio::test]
    async fn message_handler_with_local_and_forward_not_ack_topic() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    },
                )
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "pattern/p1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        handler.handle(Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let mut interval = tokio::time::interval(std::time::Duration::from_secs(1));

        if let Either::Right(_) = future::select(interval.next(), loader.next()).await {
            panic!("Should not reach here");
        }
    }

    #[tokio::test]
    async fn message_handler_with_local_and_forward_unsubscribed_topic() {
        let batch_size: usize = 5;
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    },
                )
            })
            .collect();

        let store = PublicationStore::new_memory(batch_size);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "pattern/p1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "pattern/#".into(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Unsubscribe("pattern/#".into()),
            ]))
            .await
            .unwrap();

        handler.handle(Event::Publication(pub1)).await.unwrap();

        let mut loader = handler.store.loader();

        let mut interval = tokio::time::interval(std::time::Duration::from_secs(1));

        if let Either::Right(_) = future::select(interval.next(), loader.next()).await {
            panic!("Should not reach here");
        }
    }
}
