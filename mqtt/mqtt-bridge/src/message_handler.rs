use std::cell::RefCell;
use std::convert::TryFrom;
use std::rc::Rc;

use mqtt3::{proto::Publication, Event, ReceivedPublication};
use mqtt_broker::TopicFilter;
use tracing::{debug, warn};

use crate::{
    bridge::BridgeError,
    client::EventHandler,
    persist::{PublicationStore, StreamWakeableState, WakingMemoryStore},
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
// TODO PRE: make inner a locked version of the publication store
pub struct MessageHandler<S>
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

    pub fn transform(&self, topic_name: &str) -> Option<String> {
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
                // TODO PRE: Handle error in borrow
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
