#![allow(dead_code)] // TODO remove when ready
use std::{collections::HashMap, marker::PhantomData, str::FromStr};

use anyhow::Result;
use async_trait::async_trait;
use mqtt3::{proto::Publication, Event, ReceivedPublication};
use mqtt_broker::TopicFilter;
use tracing::{debug, info, warn};

use crate::client::{EventHandler, MqttClient};
use crate::persist::memory::InMemoryPersist;
use crate::persist::Persist;
use crate::settings::{ConnectionSettings, Credentials, Topic};

const BATCH_SIZE: usize = 10;

/// Bridge implementation that connects to local broker and remote broker and handles messages flow
pub struct Bridge {
    system_address: String,
    device_id: String,
    connection_settings: ConnectionSettings,
    forwards_map: HashMap<String, Topic>,
    subs_map: HashMap<String, Topic>,
}

impl Bridge {
    pub fn new(
        system_address: String,
        device_id: String,
        connection_settings: ConnectionSettings,
    ) -> Self {
        let forwards_map: HashMap<String, Topic> = connection_settings
            .forwards()
            .iter()
            .map(move |sub| {
                let key = if let Some(local) = sub.local() {
                    format!("{}/{}", local, sub.pattern().to_string())
                } else {
                    sub.pattern().into()
                };
                (key, sub.clone())
            })
            .collect();

        let subs_map: HashMap<String, Topic> = connection_settings
            .subscriptions()
            .iter()
            .map(|sub| {
                let key = if let Some(local) = sub.local() {
                    format!("{}/{}", local, sub.pattern().to_string())
                } else {
                    sub.pattern().into()
                };
                (key, sub.clone())
            })
            .collect();

        Bridge {
            system_address,
            device_id,
            connection_settings,
            forwards_map,
            subs_map,
        }
    }

    pub async fn start(&self) -> Result<()> {
        info!("Starting bridge...{}", self.connection_settings.name());

        self.connect_to_local().await?;
        self.connect_to_remote().await?;

        Ok(())
    }

    async fn connect_to_remote(&self) -> Result<()> {
        info!(
            "connecting to broker {}",
            self.connection_settings.address()
        );

        let mut topic_filters = Vec::new();
        for val in self.subs_map.values() {
            topic_filters.push(TopicMapper {
                topic_settings: val.clone(),
                topic_filter: TopicFilter::from_str(val.pattern())?,
            });
        }

        let mut client = MqttClient::new(
            self.system_address.as_str(),
            *self.connection_settings.keep_alive(),
            self.connection_settings.clean_session(),
            MessageHandler::new(topic_filters, BATCH_SIZE),
            self.connection_settings.credentials(),
        );

        let subscriptions: Vec<String> = self.subs_map.keys().map(|key| key.into()).collect();
        debug!("subscribe to remote {:?}", subscriptions);

        client.subscribe(subscriptions).await?;
        let _events_task = tokio::spawn(client.handle_events());

        Ok(())
    }

    async fn connect_to_local(&self) -> Result<()> {
        let client_id = format!(
            "{}/$edgeHub/$bridge/{}",
            self.device_id,
            self.connection_settings.name()
        );
        info!(
            "connecting to local broker {}, clientid {}",
            self.system_address, client_id
        );

        let mut topic_filters = Vec::new();
        for val in self.forwards_map.values() {
            topic_filters.push(TopicMapper {
                topic_settings: val.clone(),
                topic_filter: TopicFilter::from_str(val.pattern())?,
            });
        }

        let mut client = MqttClient::new(
            self.system_address.as_str(),
            *self.connection_settings.keep_alive(),
            self.connection_settings.clean_session(),
            MessageHandler::new(topic_filters, BATCH_SIZE),
            &Credentials::Anonymous(client_id),
        );

        let subscriptions: Vec<String> = self.forwards_map.keys().map(|key| key.into()).collect();
        debug!("subscribe to local {:?}", subscriptions);

        client.subscribe(subscriptions).await?;

        let _events_task = tokio::spawn(client.handle_events());

        Ok(())
    }
}

#[derive(Clone)]
struct TopicMapper {
    topic_settings: Topic,
    topic_filter: TopicFilter,
}

#[derive(Clone)]
struct MessageHandler<'a, T>
where
    T: Persist<'a>,
{
    topics: Vec<TopicMapper>,
    inner: T,
    phantom: PhantomData<&'a T>,
}

impl<'a, T> MessageHandler<'a, T>
where
    T: Persist<'a>,
{
    pub fn new(topics: Vec<TopicMapper>, batch_size: usize) -> Self {
        Self {
            topics,
            inner: T::new(batch_size),
            phantom: PhantomData,
        }
    }

    fn transform(&self, topic_name: &str) -> Option<String> {
        for mapper in &self.topics {
            let pattern = mapper
                .topic_settings
                .local()
                .map_or(Some(topic_name.to_owned()), |local_prefix| {
                    if topic_name.to_owned().starts_with(local_prefix) {
                        let rs: String = topic_name
                            .strip_prefix(local_prefix)
                            .unwrap_or(&topic_name)
                            .to_string();

                        Some(rs)
                    } else {
                        None
                    }
                })
                .filter(|pattern| mapper.topic_filter.matches(pattern))
                .map(|pattern| {
                    if let Some(remote_prefix) = mapper.topic_settings.remote() {
                        format!("{}/{}", remote_prefix, pattern)
                    } else {
                        pattern
                    }
                });

            if pattern.is_some() {
                return pattern;
            }
        }
        None
    }
}

// TODO: implement for generic T where T: Persist
#[async_trait]
impl EventHandler for MessageHandler<'_, InMemoryPersist> {
    async fn handle_event(&mut self, event: Event) -> Result<()> {
        if let Event::Publication(publication) = event {
            let ReceivedPublication {
                topic_name,
                qos,
                retain,
                payload,
                dup: _,
            } = publication;
            let forward = self.transform(topic_name.as_ref()).map(|f| Publication {
                topic_name: f,
                qos,
                retain,
                payload,
            });

            if let Some(f) = forward {
                debug!("Save message to store");
                self.inner.push(f).await?;
            } else {
                warn!("No topic matched");
            }
        }

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use crate::bridge::TopicMapper;
    use crate::persist::Key;
    use bytes::Bytes;
    use futures_util::stream::StreamExt;
    use mqtt3::proto::QoS;
    use mqtt3::{Event, ReceivedPublication};
    use std::str::FromStr;

    use mqtt_broker::TopicFilter;

    use crate::bridge::{Bridge, MessageHandler};
    use crate::client::EventHandler;
    use crate::persist::{memory::InMemoryPersist, Persist};
    use crate::settings::Settings;

    #[tokio::test]
    async fn bridge_new() {
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let bridge = Bridge::new(
            "localhost:5555".into(),
            "d1".into(),
            connection_settings.clone(),
        );

        for (key, value) in &bridge.forwards_map {
            assert_eq!(key, "some");
            assert_eq!(value.remote().unwrap(), "remote");
        }

        let (key, value) = bridge.subs_map.get_key_value("temp/#").unwrap();
        assert_eq!(key, "temp/#");
        assert_eq!(value.remote().unwrap(), "floor/kitchen");

        let (key, value) = bridge.subs_map.get_key_value("pattern/#").unwrap();
        assert_eq!(key, "pattern/#");
        assert_eq!(value.remote(), None);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_forward_topic() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .subscriptions()
            .iter()
            .map(move |sub| TopicMapper {
                topic_settings: sub.clone(),
                topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
            })
            .collect();

        let mut handler = MessageHandler::<InMemoryPersist>::new(topics, batch_size);

        let pub1 = ReceivedPublication {
            topic_name: "temp/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        handler
            .handle_event(Event::Publication(pub1))
            .await
            .unwrap();

        let loader = handler.inner.loader().await;

        let extracted1 = loader.lock().next().await.unwrap();
        assert_eq!(extracted1.1.topic_name, "floor/kitchen/temp/1");
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_no_forward_mapping() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .subscriptions()
            .iter()
            .map(move |sub| TopicMapper {
                topic_settings: sub.clone(),
                topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
            })
            .collect();

        let mut handler = MessageHandler::<InMemoryPersist>::new(topics, batch_size);

        let pub1 = ReceivedPublication {
            topic_name: "pattern/p1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        handler
            .handle_event(Event::Publication(pub1))
            .await
            .unwrap();

        let loader = handler.inner.loader().await;

        let extracted1 = loader.lock().next().await.unwrap();
        assert_eq!(extracted1.1.topic_name, "pattern/p1");
    }

    #[tokio::test]
    async fn message_handler_no_topic_match() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .subscriptions()
            .iter()
            .map(move |sub| TopicMapper {
                topic_settings: sub.clone(),
                topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
            })
            .collect();

        let mut handler = MessageHandler::<InMemoryPersist>::new(topics, batch_size);

        let pub1 = ReceivedPublication {
            topic_name: "local/temp/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        handler
            .handle_event(Event::Publication(pub1))
            .await
            .unwrap();

        let key1 = Key { offset: 0 };
        assert_eq!(None, handler.inner.remove(key1).await);
    }
}
