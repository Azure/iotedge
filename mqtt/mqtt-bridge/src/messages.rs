use std::{collections::HashMap, convert::TryFrom, time::Duration};

use async_trait::async_trait;
use futures_util::{stream::Stream, StreamExt};
use mockall_double::double;
use tokio::{sync::mpsc::UnboundedSender, time};
use tracing::{debug, error, warn};

use mqtt3::{
    proto::{Publication, SubscribeTo},
    Event, SubscriptionUpdateEvent,
};
use mqtt_broker::TopicFilter;

#[double]
use crate::client::UpdateSubscriptionHandle;

use crate::{
    bridge::BridgeError,
    client::{Handled, MqttEventHandler},
    persist::{PersistError, PublicationStore, RingBufferError, StreamWakeableState},
    pump::TopicMapperUpdates,
    settings::TopicRule,
};

#[derive(Default, Clone, Debug)]
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
            .subscribe_to()
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
    retry_sub_send: Option<UnboundedSender<SubscribeTo>>,
}

impl<S> StoreMqttEventHandler<S> {
    pub fn new(store: PublicationStore<S>, topic_mappers_updates: TopicMapperUpdates) -> Self {
        Self {
            topic_mappers: HashMap::new(),
            topic_mappers_updates,
            store,
            retry_sub_send: None,
        }
    }

    pub fn set_retry_sub_sender(&mut self, sender: UnboundedSender<SubscribeTo>) {
        self.retry_sub_send = Some(sender);
    }

    fn transform(&self, topic_name: &str) -> Option<String> {
        self.topic_mappers.values().find_map(|mapper| {
            if mapper.topic_filter.matches(topic_name) {
                mapper
                    .topic_settings
                    .in_prefix()
                    // maps if local does not have a value it uses the topic that was received,
                    // else it checks that the received topic starts with local prefix and removes the local prefix
                    .map_or(Some(topic_name), |in_prefix| {
                        topic_name.strip_prefix::<&str>(in_prefix)
                    })
                    .map(|stripped_topic| match mapper.topic_settings.out_prefix() {
                        Some(out_prefix) => {
                            format!("{}{}", out_prefix, stripped_topic)
                        }
                        None => stripped_topic.to_string(),
                    })
                    .and_then(|transformed_topic| {
                        // transform_topic can be empty when topic is # and outPrefix is empty and it matches on inPrefix
                        // example topic: #, inPrefix: local/messages, outPrefix: "" and message is sent with topic local/messages
                        if transformed_topic.is_empty() {
                            warn!(
                                "topic {} was matched with {:#?}, but remote topic is not valid",
                                topic_name, mapper.topic_settings
                            );
                            None
                        } else {
                            Some(transformed_topic)
                        }
                    })
            } else {
                None
            }
        })
    }

    fn handle_subscribed(&mut self, sub: &str) {
        if let Some(mapper) = self.topic_mappers_updates.get(sub) {
            self.topic_mappers.insert(sub.to_owned(), mapper);
        } else {
            warn!("unexpected subscription ack for {}", sub);
        };
    }

    fn handle_unsubscribed(&mut self, sub: &str) {
        if self.topic_mappers.remove(sub).is_none() {
            warn!("unexpected subscription/rejected ack for {}", sub);
        };
    }

    fn handle_rejected(&mut self, sub: SubscribeTo) {
        self.topic_mappers.remove(&sub.topic_filter);
        if let Some(sender) = &mut self.retry_sub_send {
            if sender.send(sub).is_err() {
                warn!("unable to schedule subscription retry. channel closed");
            }
        }
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
                    return match self.store.push(&publication) {
                        Ok(_) => Ok(Handled::Fully),
                        Err(
                            err
                            @
                            PersistError::RingBuffer(RingBufferError::InsufficientSpace {
                                ..
                            }),
                        ) => {
                            error!(error = %err, "dropping incoming publication");
                            Ok(Handled::Fully)
                        }
                        Err(err) => Err(BridgeError::Store(err)),
                    };
                }
            }
            Event::SubscriptionUpdates(sub_updates) => {
                for update in sub_updates {
                    match update {
                        SubscriptionUpdateEvent::Subscribe(sub) => {
                            debug!("received subscribe: {:?}", sub);
                            self.handle_subscribed(&sub.topic_filter);
                        }
                        SubscriptionUpdateEvent::Unsubscribe(unsub) => {
                            debug!("received unsubscribe: {}", unsub);
                            self.handle_unsubscribed(&unsub);
                        }
                        SubscriptionUpdateEvent::RejectedByServer(sub) => {
                            warn!(
                                "received subscription rejected by broker, verify that you have permissions to subscribe to topic: {}",
                                sub.topic_filter
                            );
                            self.handle_rejected(sub.clone());
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

pub async fn retry_subscriptions<S: Stream<Item = SubscribeTo> + Unpin>(
    retries: S,
    topic_mappers_updates: TopicMapperUpdates,
    mut subscription_handle: UpdateSubscriptionHandle,
) {
    // read re-subscription requests in chunks by 100 items if ready
    let mut retries = retries.ready_chunks(100);

    while let Some(subs) = retries.next().await {
        if !subs.is_empty() {
            warn!("trying to re-subscribe to {} topics", subs.len());
            for sub in subs {
                if topic_mappers_updates.contains_key(&sub.topic_filter) {
                    warn!(
                        "re-subscribing to {} with qos {:?}",
                        sub.topic_filter, sub.qos
                    );
                    if let Err(e) = subscription_handle.subscribe(sub).await {
                        error!("failed to send subscribe {}", e);
                    }
                }
            }
        }

        // wait for timeout before trying the next batch
        time::sleep(Duration::from_secs(10)).await;
    }
}

#[cfg(test)]
mod tests {
    use std::{
        collections::HashMap,
        fmt::Debug,
        num::{NonZeroU64, NonZeroUsize},
        path::PathBuf,
        str::FromStr,
        time::Duration,
    };

    use bytes::Bytes;
    use futures_util::{stream::Stream, StreamExt, TryStreamExt};

    use mqtt3::{
        proto::{Publication, QoS, SubscribeTo},
        Event, ReceivedPublication, SubscriptionUpdateEvent,
    };
    use mqtt_broker::TopicFilter;
    use mqtt_util::{AuthenticationSettings, CredentialProviderSettings, Credentials};
    use test_case::test_case;
    use tokio::time;

    use crate::{
        client::MqttEventHandler,
        persist::{
            FlushOptions, PublicationStore, RingBuffer, StreamWakeableState, WakingMemoryStore,
        },
        pump::TopicMapperUpdates,
        settings::{
            BridgeSettings, ConnectionSettings, Direction, MemorySettings, RingBufferSettings,
            StorageSettings, TopicRule,
        },
    };

    use super::{StoreMqttEventHandler, TopicMapper};

    const FLUSH_OPTIONS: FlushOptions = FlushOptions::Off;
    const MAX_FILE_SIZE: NonZeroU64 = unsafe { NonZeroU64::new_unchecked(1024) };
    const BATCH_SIZE: NonZeroUsize = unsafe { NonZeroUsize::new_unchecked(100) };
    const MAX_SIZE: NonZeroUsize = unsafe { NonZeroUsize::new_unchecked(1024) };

    type MemoryPublicationStore = PublicationStore<WakingMemoryStore>;

    impl Default for MemoryPublicationStore {
        fn default() -> Self {
            PublicationStore::new_memory(&MemorySettings::new(MAX_SIZE))
        }
    }

    type RingBufferPublicationStore = PublicationStore<RingBuffer>;

    impl Default for RingBufferPublicationStore {
        fn default() -> Self {
            let result = tempfile::tempdir();
            assert!(result.is_ok());
            let dir = result.unwrap();
            let dir_path = dir.path().to_path_buf();

            let result = PublicationStore::new_ring_buffer(
                &RingBufferSettings::new(MAX_FILE_SIZE, dir_path, FLUSH_OPTIONS),
                "test",
                "local",
            );
            assert!(result.is_ok());
            result.unwrap()
        }
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_updates_topic<T>(store: PublicationStore<T>)
    where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();

        let topics: HashMap<String, TopicMapper> = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.subscribe_to().as_ref()).unwrap(),
                    },
                )
            })
            .collect();

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

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_retries_rejected_topic<T>(store: PublicationStore<T>)
    where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();

        let topics: HashMap<String, TopicMapper> = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.subscribe_to().as_ref()).unwrap(),
                    },
                )
            })
            .collect();

        let (tx, mut rx) = tokio::sync::mpsc::unbounded_channel();

        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));
        handler.set_retry_sub_sender(tx);

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::RejectedByServer(SubscribeTo {
                    topic_filter: "local/floor/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        assert!(!handler.topic_mappers.contains_key("local/floor/#"));
        assert_eq!(
            rx.recv().await,
            Some(SubscribeTo {
                topic_filter: "local/floor/#".to_string(),
                qos: QoS::AtLeastOnce,
            })
        );
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_updates_topic_without_pending_update<T>(store: PublicationStore<T>)
    where
        T: StreamWakeableState + Send + Sync,
    {
        let topics = HashMap::new();

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

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_saves_message_with_local_and_forward_topic<T>(
        store: PublicationStore<T>,
    ) where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();

        let topics = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.subscribe_to().as_ref()).unwrap(),
                    },
                )
            })
            .collect();

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

        let mut loader = handler.store.loader(BATCH_SIZE);

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_saves_message_with_local_and_multileveltopic<T>(
        store: PublicationStore<T>,
    ) where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();
        let topics = forwards_topics_from_settings(connection_settings);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "local/telemetry/".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };
        let expected1 = Publication {
            topic_name: "remote/messages/".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let pub2 = ReceivedPublication {
            topic_name: "local/floor4".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };
        let expected2 = Publication {
            topic_name: "floor4".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "local/telemetry/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "local/floor4/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        handler.handle(Event::Publication(pub1)).await.unwrap();
        handler.handle(Event::Publication(pub2)).await.unwrap();

        let mut loader = handler.store.loader(BATCH_SIZE);
        let extracted1 = loader.try_next().await.unwrap().unwrap();
        let extracted2 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected1);
        assert_eq!(extracted2.1, expected2);
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_saves_message_with_remote_and_multileveltopic<T>(
        store: PublicationStore<T>,
    ) where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();
        let topics = forwards_topics_from_settings(connection_settings);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "floor3".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };
        let expected = Publication {
            topic_name: "remote/messages/floor3".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "floor3/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        handler.handle(Event::Publication(pub1)).await.unwrap();
        let mut loader = handler.store.loader(BATCH_SIZE);
        let extracted = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted.1, expected);
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_saves_message_justmultileveltopic<T>(store: PublicationStore<T>)
    where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();
        let topics = forwards_topics_from_settings(connection_settings);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "floor5".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };
        let expected = Publication {
            topic_name: "floor5".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "floor5/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        handler.handle(Event::Publication(pub1)).await.unwrap();
        let mut loader = handler.store.loader(BATCH_SIZE);
        let extracted = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted.1, expected);
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_saves_message_emptytopic<T>(store: PublicationStore<T>)
    where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();
        let topics = forwards_topics_from_settings(connection_settings);
        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "foo/bar".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };
        let expected = Publication {
            topic_name: "bar/foo".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "foo/bar".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        handler.handle(Event::Publication(pub1)).await.unwrap();
        let mut loader = handler.store.loader(BATCH_SIZE);
        let extracted = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted.1, expected);
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_saves_message_with_empty_local_and_forward_topic<T>(
        store: PublicationStore<T>,
    ) where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();

        let topics = forwards_topics_from_settings(connection_settings);

        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "floor2/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        let expected1 = Publication {
            topic_name: "floor2/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let pub2 = ReceivedPublication {
            topic_name: "/floor2-2".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        let expected2 = Publication {
            topic_name: "/floor2-2".to_string(),
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

        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "/floor2-2".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();

        handler.handle(Event::Publication(pub1)).await.unwrap();
        handler.handle(Event::Publication(pub2)).await.unwrap();

        let mut loader = handler.store.loader(BATCH_SIZE);

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected1);
        let extracted2 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted2.1, expected2);
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_saves_message_with_forward_topic<T>(store: PublicationStore<T>)
    where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();

        let topics = forwards_topics_from_settings(connection_settings);

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

        let mut loader = handler.store.loader(BATCH_SIZE);

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_saves_message_with_no_forward_mapping<T>(store: PublicationStore<T>)
    where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();

        let topics = forwards_topics_from_settings(connection_settings);

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

        let mut loader = handler.store.loader(BATCH_SIZE);

        let extracted1 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.1, expected);
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_no_topic_match<T>(store: PublicationStore<T>)
    where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();

        let topics = forwards_topics_from_settings(connection_settings);

        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "local/temp/1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        let pub2 = ReceivedPublication {
            topic_name: "just/local".to_string(),
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
        handler
            .handle(Event::SubscriptionUpdates(vec![
                SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                    topic_filter: "just/local/#".to_string(),
                    qos: QoS::AtLeastOnce,
                }),
            ]))
            .await
            .unwrap();
        handler.handle(Event::Publication(pub1)).await.unwrap();
        handler.handle(Event::Publication(pub2)).await.unwrap();

        assert_empty(handler.store.loader(BATCH_SIZE)).await;
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_with_local_and_forward_not_ack_topic<T>(store: PublicationStore<T>)
    where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();

        let topics = forwards_topics_from_settings(connection_settings);

        let mut handler = StoreMqttEventHandler::new(store, TopicMapperUpdates::new(topics));

        let pub1 = ReceivedPublication {
            topic_name: "pattern/p1".to_string(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: Bytes::new(),
            dup: false,
        };

        handler.handle(Event::Publication(pub1)).await.unwrap();

        assert_empty(handler.store.loader(BATCH_SIZE)).await;
    }

    #[test_case(MemoryPublicationStore::default())]
    #[test_case(RingBufferPublicationStore::default())]
    #[tokio::test]
    async fn message_handler_with_local_and_forward_unsubscribed_topic<T>(
        store: PublicationStore<T>,
    ) where
        T: StreamWakeableState + Send + Sync,
    {
        let settings = test_bridge_settings();
        let connection_settings = settings.upstream().unwrap();

        let topics = forwards_topics_from_settings(connection_settings);

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

        assert_empty(handler.store.loader(BATCH_SIZE)).await;
    }

    async fn assert_empty<S: Stream<Item = T> + Unpin, T: Debug>(mut stream: S) {
        time::timeout(Duration::from_millis(500), stream.next())
            .await
            .expect_err("expected empty stream");
    }

    fn test_bridge_settings() -> BridgeSettings {
        BridgeSettings::new(
            Some(ConnectionSettings::new(
                "$upstream",
                "edge1:8883",
                Credentials::Provider(CredentialProviderSettings::new(
                    "my_iothub",
                    "edge1",
                    "device1",
                    "m1",
                    "123",
                    "workload",
                )),
                vec![
                    Direction::Both(TopicRule::new(
                        "temp/#",
                        None,
                        Some("floor/kitchen/".into()),
                    )),
                    Direction::Out(TopicRule::new(
                        "floor/#",
                        Some("local/".into()),
                        Some("remote/".into()),
                    )),
                    Direction::Out(TopicRule::new("pattern/#", None, None)),
                    Direction::Out(TopicRule::new("floor2/#", Some("".into()), Some("".into()))),
                    Direction::Out(TopicRule::new(
                        "/floor2-2",
                        Some("".into()),
                        Some("".into()),
                    )),
                    Direction::Out(TopicRule::new(
                        "#",
                        Some("local/telemetry/".into()),
                        Some("remote/messages/".into()),
                    )),
                    Direction::Out(TopicRule::new("#", Some("just/local/".into()), None)),
                    Direction::Out(TopicRule::new(
                        "floor3/#",
                        None,
                        Some("remote/messages/".into()),
                    )),
                    Direction::Out(TopicRule::new("floor4/#", Some("local/".into()), None)),
                    Direction::Out(TopicRule::new("floor5/#", None, None)),
                    Direction::Out(TopicRule::new(
                        "",
                        Some("foo/bar".into()),
                        Some("bar/foo".into()),
                    )),
                ],
                Duration::from_secs(60),
                false,
            )),
            vec![ConnectionSettings::new(
                "r1",
                "remote:8883",
                Credentials::PlainText(AuthenticationSettings::new(
                    "client", "mymodule", "pass", None,
                )),
                vec![
                    Direction::In(TopicRule::new(
                        "temp/#",
                        None,
                        Some("floor/kitchen/".into()),
                    )),
                    Direction::Out(TopicRule::new("some", None, Some("remote/".into()))),
                ],
                Duration::from_secs(60),
                false,
            )],
            StorageSettings::RingBuffer(RingBufferSettings::new(
                NonZeroU64::new(33_554_432).expect("33554432"), //32mb
                PathBuf::from("/tmp_file/mqttd/"),
                FlushOptions::AfterEachWrite,
            )),
        )
    }

    fn forwards_topics_from_settings(
        connection_settings: &crate::settings::ConnectionSettings,
    ) -> HashMap<String, TopicMapper> {
        let topics = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    sub.subscribe_to(),
                    TopicMapper {
                        topic_settings: sub.clone(),
                        topic_filter: TopicFilter::from_str(sub.subscribe_to().as_ref()).unwrap(),
                    },
                )
            })
            .collect();
        topics
    }
}
