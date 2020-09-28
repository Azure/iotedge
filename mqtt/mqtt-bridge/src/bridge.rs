#![allow(unused_imports)] // TODO: Remove when ready
#![allow(dead_code)] // TODO: Remove when ready
#![allow(unused_variables)] // TODO: Remove when ready

use std::cell::RefCell;
use std::rc::Rc;
use std::{collections::HashMap, convert::TryFrom, convert::TryInto, sync::Arc, time::Duration};

use async_trait::async_trait;
use mqtt3::{proto::Publication, Event, ReceivedPublication};
use mqtt_broker::TopicFilter;
use tokio::sync::Mutex;
use tracing::{debug, info, warn};

use crate::{
    client::{ClientError, EventHandler, MqttClient},
    persist::{
        MessageLoader, PersistError, PublicationStore, StreamWakeableState, WakingMemoryStore,
    },
    settings::{ConnectionSettings, Credentials, TopicRule},
};

const BATCH_SIZE: usize = 10;

// TODO PRE: make this generic
struct Pump {
    client: MqttClient<MessageHandler<WakingMemoryStore>>,
    subscriptions: Vec<String>,
    loader: Arc<Mutex<MessageLoader<WakingMemoryStore>>>,
}

impl Pump {
    pub fn new(
        client: MqttClient<MessageHandler<WakingMemoryStore>>,
        subscriptions: Vec<String>,
        loader: Arc<Mutex<MessageLoader<WakingMemoryStore>>>,
        persist: Rc<RefCell<PublicationStore<WakingMemoryStore>>>,
    ) -> Self {
        Self {
            client,
            subscriptions,
            loader,
        }
    }

    pub fn run(self) {}

    /*
    impl Pump {
        async fn run(self, shutdown) {
            let publish_handle = client.publish_handle();
            let f1 = async {
                while let Some(p) = select(shdutdown_rx.next(), self.loader.try_next()).await {
                    Left(_, loader_next) => {
                        // wait until all inflights are sent
                        let _ = senders.try_collect().await;

                    }
                    Right(p, _) => {
                        // send pubs if there is a spots in the inflight queue
                        if senders.len() < MAX_INFLIGHT {
                            let fut = async {
                                client.publish(p).await;
                                store.remove(k);
                            };
                            senders.push(fut);
                        } else {
                            senders.next().await;
                        }
                    }
                }
            };
            // if it is a mqtt3::Client
            // let f2 = async {
            //     while let Some(p) = self.client.next().await {
            //         self.queue.push(p)
            //     }
            // }
            let f2 = self.client.handle_events();
            // if we decide to go with external shutdown event
            // match select3(f1, f2, shutdown).await {
            //     Either::3(_) => {
            //         self.client.shutdown().await;
            //         shdutdown_tx.send(())
            //     }
            // }

            // if we want to chain ShuddownHandles till the one of mqtt3::Client::ShutdownHandle
            match select3(f1, f2).await {
                Either::Left(_) => {
                    shdutdown_tx.send(())
                },
                Either::Right(_) => {
                    panic!("loader errored out!!!!")
                }
            }
        }
    }
    */
}

/// Bridge implementation that connects to local broker and remote broker and handles messages flow
// TODO PRE: make persistence generic
pub struct Bridge {
    local_pump: Pump,
    remote_pump: Pump,
    connection_settings: ConnectionSettings,
}

impl Bridge {
    pub fn new(
        system_address: String,
        device_id: String,
        connection_settings: ConnectionSettings,
    ) -> Result<Self, BridgeError> {
        let mut forwards: HashMap<String, TopicRule> = connection_settings
            .forwards()
            .iter()
            .map(|sub| Self::format_key_value(sub))
            .collect();

        let mut subscriptions: HashMap<String, TopicRule> = connection_settings
            .subscriptions()
            .iter()
            .map(|sub| Self::format_key_value(sub))
            .collect();

        let mut outgoing_persist = PublicationStore::new_memory(BATCH_SIZE);
        let mut incoming_persist = PublicationStore::new_memory(BATCH_SIZE);
        let outgoing_loader = outgoing_persist.loader();
        let incoming_loader = incoming_persist.loader();
        let incoming_persist = Rc::new(RefCell::new(incoming_persist));
        let outgoing_persist = Rc::new(RefCell::new(outgoing_persist));

        // create local and remote clients
        // note: if we instead do this in start then we will have to pass the persistor into it, necessitating shared ownership and mutex

        // self.connect(
        //     self.subscriptions.clone(),
        //     self.incoming_persist,
        //     self.connection_settings.address(),
        //     Some(self.connection_settings.port().to_owned()),
        //     self.connection_settings.credentials(),
        //     true,
        // )
        // .await
        // self.connect(
        //     self.forwards.clone(),
        //     self.outgoing_persist,
        //     self.system_address.as_str(),
        //     None,
        //     &Credentials::Anonymous(client_id),
        //     false,
        // )
        // .await
        // async fn connect(
        //     &self,
        //     mut topics: HashMap<String, TopicRule>,
        //     persistor: PublicationStore<WakingMemoryStore>,
        //     address: &str,
        //     port: Option<String>,
        //     credentials: &Credentials,
        //     secure: bool,
        // ) -> Result<(), BridgeError> {
        // let client = MqttClient::new(
        //     address,
        //     port,
        //     self.connection_settings.keep_alive(),
        //     self.connection_settings.clean_session(),
        //     MessageHandler::new(persistor, topic_filters),
        //     credentials,
        //     secure,
        // );

        let (remote_subscriptions, remote_topic_rules): (Vec<_>, Vec<_>) =
            subscriptions.drain().unzip();
        let remote_topic_filters = remote_topic_rules
            .into_iter()
            .map(|topic| topic.try_into())
            .collect::<Result<Vec<_>, _>>()?;
        let remote_client = MqttClient::new(
            connection_settings.address(),
            Some(connection_settings.port().to_owned()),
            connection_settings.keep_alive(),
            connection_settings.clean_session(),
            MessageHandler::new(incoming_persist.clone(), remote_topic_filters),
            connection_settings.credentials(),
            true,
        );

        let local_client_id = format!(
            "{}/$edgeHub/$bridge/{}",
            device_id,
            connection_settings.name()
        );
        let (local_subscriptions, local_topic_rules): (Vec<_>, Vec<_>) = forwards.drain().unzip();
        let local_topic_filters = local_topic_rules
            .into_iter()
            .map(|topic| topic.try_into())
            .collect::<Result<Vec<_>, _>>()?;
        let local_client = MqttClient::new(
            system_address.as_str(),
            None,
            connection_settings.keep_alive(),
            connection_settings.clean_session(),
            MessageHandler::new(outgoing_persist.clone(), local_topic_filters),
            &Credentials::Anonymous(local_client_id),
            true,
        );

        let local_pump = Pump::new(
            local_client,
            local_subscriptions,
            incoming_loader,
            outgoing_persist,
        );
        let remote_pump = Pump::new(
            remote_client,
            remote_subscriptions,
            outgoing_loader,
            incoming_persist,
        );

        // TODO PRE: remove persistors from self
        Ok(Bridge {
            local_pump,
            remote_pump,
            connection_settings,
        })
    }

    fn format_key_value(topic: &TopicRule) -> (String, TopicRule) {
        let key = if let Some(local) = topic.local() {
            format!("{}/{}", local, topic.pattern().to_string())
        } else {
            topic.pattern().into()
        };
        (key, topic.clone())
    }

    pub async fn start(&self) -> Result<(), BridgeError> {
        info!("Starting bridge...{}", self.connection_settings.name());

        // self.connect_to_local().await?;
        // self.connect_to_remote().await?;

        Ok(())
    }

    // async fn connect_to_remote(&self) -> Result<(), BridgeError> {
    //     info!(
    //         "connecting to remote broker {}",
    //         self.connection_settings.address()
    //     );

    //     self.connect(
    //         self.subscriptions.clone(),
    //         self.incoming_persist,
    //         self.connection_settings.address(),
    //         Some(self.connection_settings.port().to_owned()),
    //         self.connection_settings.credentials(),
    //         true,
    //     )
    //     .await
    // }

    // async fn connect_to_local(&self) -> Result<(), BridgeError> {
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
    //         self.outgoing_persist,
    //         self.system_address.as_str(),
    //         None,
    //         &Credentials::Anonymous(client_id),
    //         false,
    //     )
    //     .await
    // }

    // async fn connect(
    //     &self,
    //     mut topics: HashMap<String, TopicRule>,
    //     persistor: PublicationStore<WakingMemoryStore>,
    //     address: &str,
    //     port: Option<String>,
    //     credentials: &Credentials,
    //     secure: bool,
    // ) -> Result<(), BridgeError> {
    //     let (subscriptions, topics): (Vec<_>, Vec<_>) = topics.drain().unzip();
    //     let topic_filters = topics
    //         .into_iter()
    //         .map(|topic| topic.try_into())
    //         .collect::<Result<Vec<_>, _>>()?;

    //     let client = MqttClient::new(
    //         address,
    //         port,
    //         self.connection_settings.keep_alive(),
    //         self.connection_settings.clean_session(),
    //         MessageHandler::new(persistor, topic_filters),
    //         credentials,
    //         secure,
    //     );

    //     debug!("subscribe to remote {:?}", subscriptions);
    // client
    // .subscribe(subscriptions)
    // .await
    // .map_err(BridgeError::Subscribe)?;

    //     //TODO: handle this with shutdown
    //     let _events_task = tokio::spawn(client.handle_events());

    //     // TODO PRE: start new thread where client starts pump

    //     Ok(())
    // }
}

#[derive(Clone)]
struct TopicMapper {
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
// TODO PRE: make inner a locked version of the publication store
struct MessageHandler<S>
where
    S: StreamWakeableState,
{
    topic_mappers: Vec<TopicMapper>,
    inner: Rc<RefCell<PublicationStore<S>>>,
}

impl<S> MessageHandler<S>
where
    S: StreamWakeableState,
{
    pub fn new(
        persistor: Rc<RefCell<PublicationStore<S>>>,
        topic_mappers: Vec<TopicMapper>,
    ) -> Self {
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
impl EventHandler for MessageHandler<WakingMemoryStore> {
    type Error = BridgeError;

    fn handle_event(&mut self, event: Event) -> Result<(), Self::Error> {
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
                let mut publication_store = self.inner.borrow_mut();
                publication_store.push(f).map_err(BridgeError::Store)?;
                drop(publication_store);
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
    Subscribe(#[from] ClientError),

    #[error("failed to parse topic pattern.")]
    TopicFilterParse(#[from] mqtt_broker::Error),

    #[error("failed to load settings.")]
    LoadingSettings(#[from] config::ConfigError),
}

// #[cfg(test)]
// mod tests {
//     use bytes::Bytes;
//     use futures_util::stream::StreamExt;
//     use futures_util::stream::TryStreamExt;
//     use std::str::FromStr;

//     use mqtt3::{
//         proto::{Publication, QoS},
//         Event, ReceivedPublication,
//     };
//     use mqtt_broker::TopicFilter;

//     use crate::bridge::{Bridge, MessageHandler, TopicMapper};
//     use crate::client::EventHandler;
//     use crate::persist::PublicationStore;
//     use crate::settings::Settings;

//     #[tokio::test]
//     async fn bridge_new() {
//         let settings = Settings::from_file("tests/config.json").unwrap();
//         let connection_settings = settings.upstream().unwrap();

//         let bridge = Bridge::new(
//             "localhost:5555".into(),
//             "d1".into(),
//             connection_settings.clone(),
//         );

//         let (key, value) = bridge.forwards.get_key_value("temp/#").unwrap();
//         assert_eq!(key, "temp/#");
//         assert_eq!(value.remote().unwrap(), "floor/kitchen");
//         assert_eq!(value.local(), None);

//         let (key, value) = bridge.forwards.get_key_value("pattern/#").unwrap();
//         assert_eq!(key, "pattern/#");
//         assert_eq!(value.remote(), None);

//         let (key, value) = bridge.forwards.get_key_value("local/floor/#").unwrap();
//         assert_eq!(key, "local/floor/#");
//         assert_eq!(value.local().unwrap(), "local");
//         assert_eq!(value.remote().unwrap(), "remote");

//         let (key, value) = bridge.subscriptions.get_key_value("temp/#").unwrap();
//         assert_eq!(key, "temp/#");
//         assert_eq!(value.remote().unwrap(), "floor/kitchen");
//     }

//     #[tokio::test]
//     async fn message_handler_saves_message_with_local_and_forward_topic() {
//         let batch_size: usize = 5;
//         let settings = Settings::from_file("tests/config.json").unwrap();
//         let connection_settings = settings.upstream().unwrap();

//         let topics: Vec<TopicMapper> = connection_settings
//             .forwards()
//             .iter()
//             .map(move |sub| TopicMapper {
//                 topic_settings: sub.clone(),
//                 topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
//             })
//             .collect();

//         let persistor = PublicationStore::new_memory(batch_size);
//         let mut handler = MessageHandler::new(persistor, topics);

//         let pub1 = ReceivedPublication {
//             topic_name: "local/floor/1".to_string(),
//             qos: QoS::AtLeastOnce,
//             retain: true,
//             payload: Bytes::new(),
//             dup: false,
//         };

//         let expected = Publication {
//             topic_name: "remote/floor/1".to_string(),
//             qos: QoS::AtLeastOnce,
//             retain: true,
//             payload: Bytes::new(),
//         };

//         handler
//             .handle_event(Event::Publication(pub1))
//             .await
//             .unwrap();

//         let loader = handler.inner.loader();

//         let extracted1 = loader.lock().try_next().await.unwrap().unwrap();
//         assert_eq!(extracted1.1, expected);
//     }

//     #[tokio::test]
//     async fn message_handler_saves_message_with_forward_topic() {
//         let batch_size: usize = 5;
//         let settings = Settings::from_file("tests/config.json").unwrap();
//         let connection_settings = settings.upstream().unwrap();

//         let topics: Vec<TopicMapper> = connection_settings
//             .forwards()
//             .iter()
//             .map(move |sub| TopicMapper {
//                 topic_settings: sub.clone(),
//                 topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
//             })
//             .collect();

//         let persistor = PublicationStore::new_memory(batch_size);
//         let mut handler = MessageHandler::new(persistor, topics);

//         let pub1 = ReceivedPublication {
//             topic_name: "temp/1".to_string(),
//             qos: QoS::AtLeastOnce,
//             retain: true,
//             payload: Bytes::new(),
//             dup: false,
//         };

//         let expected = Publication {
//             topic_name: "floor/kitchen/temp/1".to_string(),
//             qos: QoS::AtLeastOnce,
//             retain: true,
//             payload: Bytes::new(),
//         };

//         handler
//             .handle_event(Event::Publication(pub1))
//             .await
//             .unwrap();

//         let loader = handler.inner.loader();

//         let extracted1 = loader.lock().try_next().await.unwrap().unwrap();
//         assert_eq!(extracted1.1, expected);
//     }

//     #[tokio::test]
//     async fn message_handler_saves_message_with_no_forward_mapping() {
//         let batch_size: usize = 5;
//         let settings = Settings::from_file("tests/config.json").unwrap();
//         let connection_settings = settings.upstream().unwrap();

//         let topics: Vec<TopicMapper> = connection_settings
//             .forwards()
//             .iter()
//             .map(move |sub| TopicMapper {
//                 topic_settings: sub.clone(),
//                 topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
//             })
//             .collect();

//         let persistor = PublicationStore::new_memory(batch_size);
//         let mut handler = MessageHandler::new(persistor, topics);

//         let pub1 = ReceivedPublication {
//             topic_name: "pattern/p1".to_string(),
//             qos: QoS::AtLeastOnce,
//             retain: true,
//             payload: Bytes::new(),
//             dup: false,
//         };

//         let expected = Publication {
//             topic_name: "pattern/p1".to_string(),
//             qos: QoS::AtLeastOnce,
//             retain: true,
//             payload: Bytes::new(),
//         };

//         handler
//             .handle_event(Event::Publication(pub1))
//             .await
//             .unwrap();

//         let loader = handler.inner.loader();

//         let extracted1 = loader.lock().try_next().await.unwrap().unwrap();
//         assert_eq!(extracted1.1, expected);
//     }

//     #[tokio::test]
//     async fn message_handler_no_topic_match() {
//         let batch_size: usize = 5;
//         let settings = Settings::from_file("tests/config.json").unwrap();
//         let connection_settings = settings.upstream().unwrap();

//         let topics: Vec<TopicMapper> = connection_settings
//             .forwards()
//             .iter()
//             .map(move |sub| TopicMapper {
//                 topic_settings: sub.clone(),
//                 topic_filter: TopicFilter::from_str(sub.pattern()).unwrap(),
//             })
//             .collect();

//         let persistor = PublicationStore::new_memory(batch_size);
//         let mut handler = MessageHandler::new(persistor, topics);

//         let pub1 = ReceivedPublication {
//             topic_name: "local/temp/1".to_string(),
//             qos: QoS::AtLeastOnce,
//             retain: true,
//             payload: Bytes::new(),
//             dup: false,
//         };

//         handler
//             .handle_event(Event::Publication(pub1))
//             .await
//             .unwrap();

//         let loader = handler.inner.loader();

//         let mut interval = tokio::time::interval(std::time::Duration::from_secs(1));
//         futures_util::future::select(interval.next(), loader.lock().next()).await;
//     }
// }
