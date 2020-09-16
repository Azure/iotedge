use std::{collections::HashMap, marker::PhantomData, str::FromStr, time::Duration};

use async_trait::async_trait;
use mqtt3::{proto::Publication, Event, ReceivedPublication};
use mqtt_broker::TopicFilter;
use tracing::{debug, info, warn};

use crate::client::{ClientConnectError, EventHandler, MqttClient};
use crate::persist::{memory::InMemoryPersist, Persist};
use crate::settings::{ConnectionSettings, Credentials, Topic};

const BATCH_SIZE: usize = 10;

/// Bridge implementation that connects to local broker and remote broker and handles messages flow
pub struct Bridge {
    system_address: String,
    device_id: String,
    connection_settings: ConnectionSettings,
    forwards: HashMap<String, Topic>,
    subscriptions: HashMap<String, Topic>,
}

impl Bridge {
    pub fn new(
        system_address: String,
        device_id: String,
        connection_settings: ConnectionSettings,
    ) -> Self {
        let forwards = connection_settings
            .forwards()
            .iter()
            .map(|sub| Self::format_key_value(sub))
            .collect();

        let subscriptions = connection_settings
            .subscriptions()
            .iter()
            .map(|sub| Self::format_key_value(sub))
            .collect();

        Bridge {
            system_address,
            device_id,
            connection_settings,
            forwards,
            subscriptions,
        }
    }

    fn format_key_value(topic: &Topic) -> (String, Topic) {
        let key = if let Some(local) = topic.local() {
            format!("{}/{}", local, topic.pattern().to_string())
        } else {
            topic.pattern().into()
        };
        (key, topic.clone())
    }

    pub async fn start(&self) -> Result<(), BridgeError> {
        info!("Starting bridge...{}", self.connection_settings.name());

        self.connect_to_local().await?;
        self.connect_to_remote().await?;

        Ok(())
    }

    async fn connect_to_remote(&self) -> Result<(), BridgeError> {
        info!(
            "connecting to remote broker {}",
            self.connection_settings.address()
        );

        self.connect(
            &self.subscriptions,
            self.connection_settings.address(),
            *self.connection_settings.keep_alive(),
            self.connection_settings.clean_session(),
            self.connection_settings.credentials(),
        )
        .await
    }

    async fn connect_to_local(&self) -> Result<(), BridgeError> {
        let client_id = format!(
            "{}/$edgeHub/$bridge/{}",
            self.device_id,
            self.connection_settings.name()
        );
        info!(
            "connecting to local broker {}, clientid {}",
            self.system_address, client_id
        );

        self.connect(
            &self.forwards,
            self.system_address.as_str(),
            *self.connection_settings.keep_alive(),
            self.connection_settings.clean_session(),
            &Credentials::Anonymous(client_id),
        )
        .await
    }

    async fn connect(
        &self,
        topics: &HashMap<String, Topic>,
        address: &str,
        keep_alive: Duration,
        clean_session: bool,
        credentials: &Credentials,
    ) -> Result<(), BridgeError> {
        let mut topic_filters = Vec::new();
        for val in topics.values() {
            topic_filters.push(TopicMapper {
                topic_settings: val.clone(),
                topic_filter: TopicFilter::from_str(val.pattern())
                    .map_err(BridgeError::TopicFilterParseError)?,
            });
        }

        let mut client = MqttClient::new(
            address,
            keep_alive,
            clean_session,
            MessageHandler::new(topic_filters, BATCH_SIZE),
            credentials,
        );

        let subscriptions: Vec<String> = topics.keys().map(|key| key.into()).collect();
        debug!("subscribe to remote {:?}", subscriptions);

        client
            .subscribe(subscriptions)
            .await
            .map_err(BridgeError::SubscribeError)?;

        //TODO: handle this with shutdown
        let _events_task = tokio::spawn(client.handle_events());

        Ok(())
    }
}

#[derive(Clone)]
struct TopicMapper {
    topic_settings: Topic,
    topic_filter: TopicFilter,
}

/// Handle events from client and saves them with the forward topic
#[derive(Clone)]
struct MessageHandler<'a, T>
where
    T: Persist<'a>,
{
    topic_mappers: Vec<TopicMapper>,
    inner: T,
    phantom: PhantomData<&'a T>,
}

impl<'a, T> MessageHandler<'a, T>
where
    T: Persist<'a>,
{
    pub fn new(topic_mappers: Vec<TopicMapper>, batch_size: usize) -> Self {
        Self {
            topic_mappers,
            inner: T::new(batch_size),
            phantom: PhantomData,
        }
    }

    fn transform(&self, topic_name: &str) -> Option<String> {
        for mapper in &self.topic_mappers {
            let forward_topic = mapper
                .topic_settings
                .local()
                // maps if local does not have a value it uses the topic that was received,
                // else it checks that the received topic starts with local prefix and removes the local prefix
                .map_or(Some(topic_name.to_owned()), |local_prefix| {
                    let prefix = format!("{}/", local_prefix);
                    if topic_name.to_owned().starts_with(&prefix) {
                        let rs: String = topic_name
                            .strip_prefix(&prefix)
                            .unwrap_or(&topic_name)
                            .to_owned();

                        Some(rs)
                    } else {
                        // is no match if there is a local prefix for the mapper but received topic does not start with it
                        None
                    }
                })
                // match topic without local prefix with the topic filter pattern
                .filter(|stripped_topic| mapper.topic_filter.matches(stripped_topic))
                .map(|stripped_topic| {
                    if let Some(remote_prefix) = mapper.topic_settings.remote() {
                        format!("{}/{}", remote_prefix, stripped_topic)
                    } else {
                        stripped_topic
                    }
                });

            if forward_topic.is_some() {
                return forward_topic;
            }
        }
        None
    }
}

// TODO: implement for generic T where T: Persist
#[async_trait]
impl EventHandler for MessageHandler<'_, InMemoryPersist> {
    type Error = BridgeError;

    async fn handle_event(&mut self, event: Event) -> Result<(), Self::Error> {
        if let Event::Publication(publication) = event {
            let ReceivedPublication {
                topic_name,
                qos,
                retain,
                payload,
                dup: _,
            } = publication;
            let forward_publication = self.transform(topic_name.as_ref()).map(|f| Publication {
                topic_name: f,
                qos,
                retain,
                payload,
            });

            if let Some(f) = forward_publication {
                debug!("Save message to store");
                self.inner.push(f).await.map_err(BridgeError::StoreError)?;
            } else {
                warn!("No topic matched");
            }
        }

        Ok(())
    }
}

/// Authentication error.
#[derive(Debug, thiserror::Error)]
pub enum BridgeError {
    #[error("failed to save to store.")]
    StoreError(#[from] std::io::Error),

    #[error("failed to subscribe to topic.")]
    SubscribeError(#[from] ClientConnectError),

    #[error("failed to parse topic pattern.")]
    TopicFilterParseError(#[from] mqtt_broker::Error),

    #[error("failed to load settings.")]
    LoadingSettingsError(#[from] config::ConfigError),
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use futures_util::stream::StreamExt;
    use std::str::FromStr;

    use mqtt3::{
        proto::{Publication, QoS},
        Event, ReceivedPublication,
    };
    use mqtt_broker::TopicFilter;

    use crate::bridge::{Bridge, MessageHandler, TopicMapper};
    use crate::client::EventHandler;
    use crate::persist::{memory::InMemoryPersist, Key, Persist};
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

        let (key, value) = bridge.forwards.get_key_value("temp/#").unwrap();
        assert_eq!(key, "temp/#");
        assert_eq!(value.remote().unwrap(), "floor/kitchen");
        assert_eq!(value.local(), None);

        let (key, value) = bridge.forwards.get_key_value("pattern/#").unwrap();
        assert_eq!(key, "pattern/#");
        assert_eq!(value.remote(), None);

        let (key, value) = bridge.forwards.get_key_value("local/floor/#").unwrap();
        assert_eq!(key, "local/floor/#");
        assert_eq!(value.local().unwrap(), "local");
        assert_eq!(value.remote().unwrap(), "remote");

        let (key, value) = bridge.subscriptions.get_key_value("temp/#").unwrap();
        assert_eq!(key, "temp/#");
        assert_eq!(value.remote().unwrap(), "floor/kitchen");
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_local_and_forward_topic() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .forwards()
            .iter()
            .map(move |sub| TopicMapper {
                topic_settings: sub.clone(),
                topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
            })
            .collect();

        let mut handler = MessageHandler::<InMemoryPersist>::new(topics, batch_size);

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
            .handle_event(Event::Publication(pub1))
            .await
            .unwrap();

        let loader = handler.inner.loader().await;

        let extracted1 = loader.lock().next().await.unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_forward_topic() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .forwards()
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

        let expected = Publication {
            topic_name: "floor/kitchen/temp/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler
            .handle_event(Event::Publication(pub1))
            .await
            .unwrap();

        let loader = handler.inner.loader().await;

        let extracted1 = loader.lock().next().await.unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_no_forward_mapping() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .forwards()
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

        let expected = Publication {
            topic_name: "pattern/p1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler
            .handle_event(Event::Publication(pub1))
            .await
            .unwrap();

        let loader = handler.inner.loader().await;

        let extracted1 = loader.lock().next().await.unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_no_topic_match() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .forwards()
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
