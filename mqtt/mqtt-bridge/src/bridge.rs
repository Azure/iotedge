#![allow(dead_code)] // TODO remove when ready

use std::{
    collections::HashMap,
    convert::TryFrom,
    convert::TryInto,
    fmt::{Display, Formatter, Result as FmtResult},
};

use async_trait::async_trait;
use serde::{Deserialize, Serialize};
use tokio::sync::mpsc::{error::SendError, Sender};
use tracing::{debug, info, warn};

use mqtt3::{proto::Publication, Event};
use mqtt_broker::TopicFilter;

use crate::{
    client::{ClientConnectError, EventHandler, Handled, MqttClient},
    persist::{PersistError, PublicationStore, StreamWakeableState},
    rpc::{RpcError, RpcHandler},
    settings::{ConnectionSettings, Credentials, Direction, Topic},
};

const BATCH_SIZE: usize = 10;

#[derive(Debug, Serialize, Deserialize)]
pub struct BridgeUpdate {
    // TODO: add update settings here
}

#[derive(Debug, PartialEq)]
pub enum PumpMessage {
    ConnectivityUpdate(ConnectivityState),
    ConfigurationUpdate(ConnectionSettings),
}

pub struct PumpHandle {
    sender: Sender<PumpMessage>,
}

impl PumpHandle {
    pub fn new(sender: Sender<PumpMessage>) -> Self {
        Self { sender }
    }

    pub async fn send(&mut self, message: PumpMessage) -> Result<(), BridgeError> {
        self.sender
            .send(message)
            .await
            .map_err(BridgeError::SenderToPump)
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum ConnectivityState {
    Connected,
    Disconnected,
}

impl Display for ConnectivityState {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            Self::Connected => write!(f, "Connected"),
            Self::Disconnected => write!(f, "Disconnected"),
        }
    }
}

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
            .subscriptions()
            .iter()
            .filter_map(|s| {
                if *s.direction() == Direction::Out {
                    Some(Self::format_key_value(s))
                } else {
                    None
                }
            })
            .collect();

        let subscriptions = connection_settings
            .subscriptions()
            .iter()
            .filter_map(|s| {
                if *s.direction() == Direction::In {
                    Some(Self::format_key_value(s))
                } else {
                    None
                }
            })
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
        let key = if let Some(local) = topic.in_prefix() {
            format!("{}/{}", local, topic.topic().to_string())
        } else {
            topic.topic().into()
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
            self.subscriptions.clone(),
            self.connection_settings.address(),
            self.connection_settings.credentials(),
            true,
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
            self.forwards.clone(),
            self.system_address.as_str(),
            &Credentials::Anonymous(client_id),
            false,
        )
        .await
    }

    async fn connect(
        &self,
        mut topics: HashMap<String, Topic>,
        address: &str,
        credentials: &Credentials,
        secure: bool,
    ) -> Result<(), BridgeError> {
        let (subscriptions, topics): (Vec<_>, Vec<_>) = topics.drain().unzip();
        let topic_filters = topics
            .into_iter()
            .map(|topic| topic.try_into())
            .collect::<Result<Vec<_>, _>>()?;

        let persistor = PublicationStore::new_memory(BATCH_SIZE);
        let mut client = if secure {
            MqttClient::tls(
                address,
                self.connection_settings.keep_alive(),
                self.connection_settings.clean_session(),
                MessageHandler::new(persistor, topic_filters),
                credentials,
            )
        } else {
            MqttClient::tcp(
                address,
                self.connection_settings.keep_alive(),
                self.connection_settings.clean_session(),
                MessageHandler::new(persistor, topic_filters),
                credentials,
            )
        };

        debug!("subscribe to {:?} {:?}", address.to_owned(), subscriptions);

        client
            .subscribe(subscriptions)
            .await
            .map_err(BridgeError::Subscribe)?;

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

impl TryFrom<Topic> for TopicMapper {
    type Error = BridgeError;

    fn try_from(topic: Topic) -> Result<Self, BridgeError> {
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
struct MessageHandler<S> {
    topic_mappers: Vec<TopicMapper>,
    store: PublicationStore<S>,
}

impl<S> MessageHandler<S> {
    pub fn new(persistor: PublicationStore<S>, topic_mappers: Vec<TopicMapper>) -> Self {
        Self {
            topic_mappers,
            store: persistor,
        }
    }

    fn transform(&self, topic_name: &str) -> Option<String> {
        self.topic_mappers.iter().find_map(|mapper| {
            mapper
                .topic_settings
                .in_prefix()
                // maps if local does not have a value it uses the topic that was received,
                // else it checks that the received topic starts with local prefix and removes the local prefix
                .map_or(Some(topic_name), |local_prefix| {
                    let prefix = format!("{}/", local_prefix);
                    topic_name.strip_prefix(&prefix)
                })
                // match topic without local prefix with the topic filter pattern
                .filter(|stripped_topic| mapper.topic_filter.matches(stripped_topic))
                .map(|stripped_topic| {
                    if let Some(remote_prefix) = mapper.topic_settings.out_prefix() {
                        format!("{}/{}", remote_prefix, stripped_topic)
                    } else {
                        stripped_topic.to_string()
                    }
                })
        })
    }
}

#[async_trait]
impl<S> EventHandler for MessageHandler<S>
where
    S: StreamWakeableState + Send,
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

#[async_trait]
impl<S> EventHandler for UpstreamHandler<S>
where
    S: StreamWakeableState + Send,
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

/// Bridge error.
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

    #[error("Failed to get send pump message.")]
    SenderToPump(#[from] SendError<PumpMessage>),

    #[error("failed to execute RPC command")]
    Rpc(#[from] RpcError),
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use futures_util::stream::StreamExt;
    use futures_util::stream::TryStreamExt;
    use std::str::FromStr;

    use mqtt3::{
        proto::{Publication, QoS},
        Event, ReceivedPublication,
    };
    use mqtt_broker::TopicFilter;

    use crate::client::EventHandler;
    use crate::persist::PublicationStore;
    use crate::settings::Settings;
    use crate::{
        bridge::{Bridge, MessageHandler, TopicMapper},
        settings::Direction,
    };

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
        assert_eq!(value.out_prefix().unwrap(), "floor/kitchen");
        assert_eq!(value.in_prefix(), None);

        let (key, value) = bridge.forwards.get_key_value("pattern/#").unwrap();
        assert_eq!(key, "pattern/#");
        assert_eq!(value.out_prefix(), None);
        assert_eq!(value.in_prefix(), None);

        let (key, value) = bridge.forwards.get_key_value("local/floor/#").unwrap();
        assert_eq!(key, "local/floor/#");
        assert_eq!(value.in_prefix().unwrap(), "local");
        assert_eq!(value.out_prefix().unwrap(), "remote");

        let (key, value) = bridge.subscriptions.get_key_value("temp/#").unwrap();
        assert_eq!(key, "temp/#");
        assert_eq!(value.out_prefix().unwrap(), "floor/kitchen");
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_local_and_forward_topic() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .subscriptions()
            .iter()
            .filter_map(|sub| {
                if *sub.direction() == Direction::Out {
                    Some(TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    })
                } else {
                    None
                }
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

        handler.handle(&Event::Publication(pub1)).await.unwrap();

        let loader = handler.store.loader();

        let extracted1 = loader.lock().try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_forward_topic() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .subscriptions()
            .iter()
            .filter_map(|sub| {
                if *sub.direction() == Direction::Out {
                    Some(TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    })
                } else {
                    None
                }
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

        handler.handle(&Event::Publication(pub1)).await.unwrap();

        let loader = handler.store.loader();

        let extracted1 = loader.lock().try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_saves_message_with_no_forward_mapping() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .subscriptions()
            .iter()
            .filter_map(|sub| {
                if *sub.direction() == Direction::Out {
                    Some(TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    })
                } else {
                    None
                }
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

        handler.handle(&Event::Publication(pub1)).await.unwrap();

        let loader = handler.store.loader();

        let extracted1 = loader.lock().try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[tokio::test]
    async fn message_handler_no_topic_match() {
        let batch_size: usize = 5;
        let settings = Settings::from_file("tests/config.json").unwrap();
        let connection_settings = settings.upstream().unwrap();

        let topics: Vec<TopicMapper> = connection_settings
            .subscriptions()
            .iter()
            .filter_map(|sub| {
                if *sub.direction() == Direction::Out {
                    Some(TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.topic()).unwrap(),
                    })
                } else {
                    None
                }
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

        handler.handle(&Event::Publication(pub1)).await.unwrap();

        let loader = handler.store.loader();

        let mut interval = tokio::time::interval(std::time::Duration::from_secs(1));
        futures_util::future::select(interval.next(), loader.lock().next()).await;
    }
}
