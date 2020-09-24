use std::{collections::HashMap, convert::TryFrom, convert::TryInto, sync::Arc, time::Duration};

use async_trait::async_trait;
use mqtt3::{proto::Publication, Event, ReceivedPublication};
use mqtt_broker::TopicFilter;
use parking_lot::Mutex;
use tracing::{debug, info, warn};

use crate::{
    client::{ClientConnectError, EventHandler, MqttClient},
    persist::{
        MessageLoader, PersistError, PublicationStore, StreamWakeableState, WakingMemoryStore,
    },
    settings::{ConnectionSettings, Credentials, Topic},
};

const BATCH_SIZE: usize = 10;

/// Bridge implementation that connects to local broker and remote broker and handles messages flow
// TODO PRE: make persistence generic
pub struct Bridge {
    system_address: String,
    device_id: String,
    connection_settings: ConnectionSettings,
    forwards: HashMap<String, Topic>,
    subscriptions: HashMap<String, Topic>,
    outgoing_persist: PublicationStore<WakingMemoryStore>,
    incoming_persist: PublicationStore<WakingMemoryStore>,
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

        let outgoing_persist = PublicationStore::new_memory(BATCH_SIZE);
        let incoming_persist = PublicationStore::new_memory(BATCH_SIZE);

        Bridge {
            system_address,
            device_id,
            connection_settings,
            forwards,
            subscriptions,
            outgoing_persist,
            incoming_persist,
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

    pub async fn start(self) -> Result<(), BridgeError> {
        info!("Starting bridge...{}", self.connection_settings.name());

        // let outgoing_persist = &mut self.outgoing_persist;
        // let incoming_persist = &mut self.incoming_persist;
        // let incoming_loader = incoming_persist.loader();
        // let outgoing_loader = outgoing_persist.loader();

        let mut outgoing_persist = self.outgoing_persist;
        let mut incoming_persist = self.incoming_persist;
        let incoming_loader = incoming_persist.loader();
        let outgoing_loader = outgoing_persist.loader();

        // self.connect_to_local(outgoing_persist, incoming_loader)
        //     .await?;
        // self.connect_to_remote(incoming_persist, outgoing_loader)
        //     .await?;

        info!(
            "connecting to remote broker {}",
            self.connection_settings.address()
        );
        connect(
            self.subscriptions.clone(),
            incoming_persist,
            outgoing_loader,
            self.connection_settings.address(),
            Some(self.connection_settings.port().to_owned()),
            self.connection_settings.keep_alive(),
            self.connection_settings.clean_session(),
            self.connection_settings.credentials(),
            true,
        )
        .await?;

        let client_id = format!(
            "{}/$edgeHub/$bridge/{}",
            self.device_id,
            self.connection_settings.name()
        );
        info!(
            "connecting to local broker {}, clientid {}",
            self.system_address, client_id
        );

        connect(
            self.forwards.clone(),
            outgoing_persist,
            incoming_loader,
            self.system_address.as_str(),
            None,
            self.connection_settings.keep_alive(),
            self.connection_settings.clean_session(),
            &Credentials::Anonymous(client_id),
            false,
        )
        .await?;

        Ok(())
    }

    // async fn connect_to_remote(
    //     &self,
    //     incoming_persist: PublicationStore<WakingMemoryStore>,
    //     outgoing_loader: Arc<Mutex<MessageLoader<WakingMemoryStore>>>,
    // ) -> Result<(), BridgeError> {
    //     info!(
    //         "connecting to remote broker {}",
    //         self.connection_settings.address()
    //     );

    //     self.connect(
    //         self.subscriptions.clone(),
    //         incoming_persist,
    //         outgoing_loader,
    //         self.connection_settings.address(),
    //         Some(self.connection_settings.port().to_owned()),
    //         self.connection_settings.keep_alive(),
    //         self.connection_settings.clean_session(),
    //         self.connection_settings.credentials(),
    //         true,
    //     )
    //     .await
    // }

    // async fn connect_to_local(
    //     &self,
    //     outgoing_persist: PublicationStore<WakingMemoryStore>,
    //     incoming_loader: Arc<Mutex<MessageLoader<WakingMemoryStore>>>,
    // ) -> Result<(), BridgeError> {
    //     let client_id = format!(
    //         "{}/$edgeHub/$bridge/{}",
    //         self.device_id,
    //         self.connection_settings.name()
    //     );
    //     info!(
    //         "connecting to local broker {}, clientid {}",
    //         self.system_address, client_id
    //     );

    //     self.connect(
    //         self.forwards.clone(),
    //         outgoing_persist,
    //         incoming_loader,
    //         self.system_address.as_str(),
    //         None,
    //         self.connection_settings.keep_alive(),
    //         self.connection_settings.clean_session(),
    //         &Credentials::Anonymous(client_id),
    //         false,
    //     )
    //     .await
    // }
}

async fn connect(
    mut topics: HashMap<String, Topic>,
    persistor: PublicationStore<WakingMemoryStore>,
    loader: Arc<Mutex<MessageLoader<WakingMemoryStore>>>,
    address: &str,
    port: Option<String>,
    keep_alive: Duration,
    clean_session: bool,
    credentials: &Credentials,
    secure: bool,
) -> Result<(), BridgeError> {
    let (subscriptions, topics): (Vec<_>, Vec<_>) = topics.drain().unzip();
    let topic_filters = topics
        .into_iter()
        .map(|topic| topic.try_into())
        .collect::<Result<Vec<_>, _>>()?;

    let mut client = MqttClient::new(
        address,
        port,
        keep_alive,
        clean_session,
        MessageHandler::new(persistor, topic_filters),
        credentials,
        secure,
    );

    debug!("subscribe to remote {:?}", subscriptions);

    client
        .subscribe(subscriptions)
        .await
        .map_err(BridgeError::Subscribe)?;

    //TODO: handle this with shutdown
    let _events_task = tokio::spawn(client.handle_events());

    // TODO PRE: start new thread where client starts pump

    Ok(())
}

#[derive(Clone)]
struct TopicMapper {
    topic_settings: Topic,
    topic_filter: TopicFilter,
}

impl TryFrom<Topic> for TopicMapper {
    type Error = BridgeError;

    fn try_from(topic: Topic) -> Result<Self, BridgeError> {
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
struct MessageHandler<S>
where
    S: StreamWakeableState,
{
    topic_mappers: Vec<TopicMapper>,
    inner: PublicationStore<S>,
}

impl<S> MessageHandler<S>
where
    S: StreamWakeableState,
{
    pub fn new(persistor: PublicationStore<S>, topic_mappers: Vec<TopicMapper>) -> Self {
        Self {
            topic_mappers,
            inner: persistor,
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

// TODO: implement for generic
#[async_trait]
impl EventHandler for MessageHandler<WakingMemoryStore> {
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
                self.inner.push(f).map_err(BridgeError::Store)?;
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
    Store(#[from] PersistError),

    #[error("failed to subscribe to topic.")]
    Subscribe(#[from] ClientConnectError),

    #[error("failed to parse topic pattern.")]
    TopicFilterParse(#[from] mqtt_broker::Error),

    #[error("failed to load settings.")]
    LoadingSettings(#[from] config::ConfigError),
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
    use crate::persist::PublicationStore;
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

        let persistor = PublicationStore::new_memory(batch_size);
        let mut handler = MessageHandler::new(persistor, topics);

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

        let loader = handler.inner.loader();

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

        let persistor = PublicationStore::new_memory(batch_size);
        let mut handler = MessageHandler::new(persistor, topics);

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

        let loader = handler.inner.loader();

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

        let persistor = PublicationStore::new_memory(batch_size);
        let mut handler = MessageHandler::new(persistor, topics);

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

        let loader = handler.inner.loader();

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

        let persistor = PublicationStore::new_memory(batch_size);
        let mut handler = MessageHandler::new(persistor, topics);

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

        let loader = handler.inner.loader();

        let mut interval = tokio::time::interval(std::time::Duration::from_secs(1));
        futures_util::future::select(interval.next(), loader.lock().next()).await;
    }
}
