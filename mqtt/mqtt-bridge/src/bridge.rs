use std::collections::{HashMap, VecDeque};
use std::str::FromStr;

use anyhow::Error;
use async_trait::async_trait;
use mqtt3::{proto::Publication, Event, ReceivedPublication};
use mqtt_broker::TopicFilter;
use tracing::{debug, info};

use crate::client::{EventHandler, MqttClient};
use crate::settings::{ConnectionSettings, Credentials};

/// Bridge implementation that connects to local broker and remote broker and handles messages flow
pub struct Bridge {
    system_address: String,
    device_id: String,
    connection_settings: ConnectionSettings,
    forwards_map: HashMap<String, String>,
    subs_map: HashMap<String, String> 
}

impl Bridge {
    pub fn new(
        system_address: String,
        device_id: String,
        connection_settings: ConnectionSettings,
    ) -> Self {
        let forwards_map: HashMap<String, String> = connection_settings
            .forwards()
            .iter()
            .map(|sub| {
                (
                    format!("{}{}", sub.local().unwrap_or(""), sub.pattern().to_string()),
                    format!("{}{}", sub.remote().unwrap_or(""), sub.pattern().to_string()),
                )
            })
            .collect();

        let subs_map: HashMap<String, String> = connection_settings
            .subscriptions()
            .iter()
            .map(|sub| {
                (
                    format!("{}{}", sub.local().unwrap_or(""), sub.pattern().to_string()),
                    format!("{}{}", sub.remote().unwrap_or(""), sub.pattern().to_string()),
                )
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

    pub async fn start(&self) -> Result<(), Error> {
        info!("Starting bridge...{}", self.connection_settings.name());

        // TODO: handle errors
        self.connect_to_local().await?;
        self.connect_to_remote().await?;

        Ok(())
    }

    async fn connect_to_remote(&self) -> Result<(), Error> {
        info!(
            "connecting to local broker {}",
            self.connection_settings.address()
        );

        let mut client = MqttClient::new(
            self.system_address.as_str(),
            *self.connection_settings.keep_alive(),
            self.connection_settings.clean_session(),
            MessageHandler::new(&self.subs_map),
            self.connection_settings.credentials(),
        );

        let subscriptions: Vec<String> = self
            .subs_map
            .keys()
            .map(|key| key.into())
            .collect();
        debug!("subscribe to remote {:?}", subscriptions);
        
        client.subscribe(subscriptions).await?;
        client.handle_events().await?;

        Ok(())
    }

    async fn connect_to_local(&self) -> Result<(), Error> {
        let client_id = format!(
            "{}/$edgeHub/$bridge/{}",
            self.device_id,
            self.connection_settings.name()
        );
        info!(
            "connecting to local broker {}, clientid {}",
            self.system_address, client_id
        );

        let mut client = MqttClient::new(
            self.system_address.as_str(),
            *self.connection_settings.keep_alive(),
            self.connection_settings.clean_session(),
            MessageHandler::new(&self.forwards_map),
            &Credentials::Anonymous(client_id),
        );

        let subscriptions: Vec<String> = self
            .forwards_map
            .keys()
            .map(|key| key.into())
            .collect();
        debug!("subscribe to local {:?}", subscriptions);
        
        client.subscribe(subscriptions).await?;
        client.handle_events().await?;

        Ok(())
    }
}

#[derive(Clone)]
pub struct MessageHandler {
    topics: HashMap<String, TopicFilter>,
    inner: VecDeque<Publication>,
}

impl<'a> MessageHandler {
    pub fn new(topics: &HashMap<String, String>) -> Self {
        let mut topic_filters: HashMap<String, TopicFilter> = HashMap::new();

        for (key, val) in topics.iter() {
            topic_filters.insert(val.into(), TopicFilter::from_str(key).unwrap());
        }

        Self {
            topics: topic_filters,
            inner: VecDeque::new(),
        }
    }

    fn transform(&self, topic_name: &'a str) -> Option<&'a str> {
        self.topics.values().find_map(move |topic| {
            if topic.matches(topic_name) {
                Some(topic_name)
            } else {
                None
            }
        })
    }
}

#[async_trait]
impl EventHandler for MessageHandler {
    type Error = Error;

    // TODO: error
    async fn handle_event(&mut self, event: Event) -> Result<(), Error> {
        if let Event::Publication(publication) = event {
            let ReceivedPublication {
                topic_name,
                qos,
                retain,
                payload,
                dup: _,
            } = publication;
            let forward = self.transform(topic_name.as_ref()).map(|f| Publication {
                topic_name: f.into(),
                qos,
                retain,
                payload,
            });

            if let Some(f) = forward {
                debug!("Save message to store");
                self.inner.push_back(f)
            } else {
                debug!("No topic matched");
            }
        }

        Ok(())
    }
}
