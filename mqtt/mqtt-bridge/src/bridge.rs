#![allow(unused_imports)] // TODO: Remove when ready
#![allow(dead_code)] // TODO: Remove when ready
#![allow(unused_variables)] // TODO: Remove when ready

use std::cell::RefCell;
use std::rc::Rc;
use std::{collections::HashMap, convert::TryFrom, convert::TryInto, sync::Arc, time::Duration};

use async_trait::async_trait;
use futures_util::future::select;
use futures_util::future::select_all;
use futures_util::future::Either;
use futures_util::future::FutureExt;
use futures_util::pin_mut;
use futures_util::select;
use futures_util::stream::FuturesUnordered;
use futures_util::stream::StreamExt;
use futures_util::stream::TryStreamExt;
use mqtt3::{proto::Publication, Event, PublishHandle, ReceivedPublication, ShutdownError};
use mqtt_broker::TopicFilter;
use tokio::sync::oneshot;
use tokio::sync::oneshot::channel;
use tokio::sync::oneshot::Receiver;
use tokio::sync::oneshot::Sender;
use tokio::sync::Mutex;
use tokio::time;
use tracing::error;
use tracing::{debug, info, warn};

use crate::{
    client::{ClientError, ClientShutdownHandle, EventHandler, MqttClient},
    message_handler::MessageHandler,
    persist::{
        MessageLoader, PersistError, PublicationStore, StreamWakeableState, WakingMemoryStore,
    },
    pump::Pump,
    settings::{ConnectionSettings, Credentials, TopicRule},
};

const BATCH_SIZE: usize = 10;

#[derive(Debug)]
pub struct BridgeShutdownHandle {
    local_shutdown: Sender<()>,
    remote_shutdown: Sender<()>,
}

impl BridgeShutdownHandle {
    pub async fn shutdown(self) -> Result<(), BridgeError> {
        self.local_shutdown
            .send(())
            .map_err(BridgeError::ShutdownBridge)?;
        self.remote_shutdown
            .send(())
            .map_err(BridgeError::ShutdownBridge)?;
        Ok(())
    }
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

        let (remote_subscriptions, remote_topic_rules): (Vec<_>, Vec<_>) =
            subscriptions.drain().unzip();
        let remote_topic_filters = remote_topic_rules
            .into_iter()
            .map(|topic| topic.try_into())
            .collect::<Result<Vec<_>, _>>()?;
        let remote_client = MqttClient::new(
            connection_settings.address(),
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
        )?;
        let remote_pump = Pump::new(
            remote_client,
            remote_subscriptions,
            outgoing_loader,
            incoming_persist,
        )?;

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

    pub async fn start(&mut self) -> Result<BridgeShutdownHandle, BridgeError> {
        info!("Starting bridge...{}", self.connection_settings.name());

        let (local_shutdown, local_shutdown_listener) = oneshot::channel::<()>();
        let (remote_shutdown, remote_shutdown_listener) = oneshot::channel::<()>();
        let shutdown_handle = BridgeShutdownHandle {
            local_shutdown,
            remote_shutdown,
        };

        self.local_pump.run(local_shutdown_listener).await;
        self.remote_pump.run(remote_shutdown_listener).await;

        Ok(shutdown_handle)
    }
}

/// Authentication error.
#[derive(Debug, thiserror::Error)]
pub enum BridgeError {
    #[error("Failed to save to store.")]
    Store(#[from] PersistError),

    #[error("Failed to subscribe to topic.")]
    Subscribe(#[source] ClientError),

    #[error("Failed to parse topic pattern.")]
    TopicFilterParse(#[from] mqtt_broker::Error),

    #[error("Failed to load settings.")]
    LoadingSettings(#[from] config::ConfigError),

    #[error("Failed to signal bridge shutdown.")]
    ShutdownBridge(()),

    #[error("Failed to get publish handle from client.")]
    PublishHandle(#[source] ClientError),

    #[error("Failed to get publish handle from client.")]
    ClientShutdown(#[from] ShutdownError),
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
