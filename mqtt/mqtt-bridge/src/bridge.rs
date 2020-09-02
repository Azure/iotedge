use std::collections::{HashMap, VecDeque};

use anyhow::Error;
use async_trait::async_trait;
use mqtt3::{proto::Publication, Event};
use tracing::{debug, info};

use crate::client::{EventHandler, MqttClient};
use crate::settings::{ConnectionSettings, Credentials};

/// Bridge implementation that connects to local broker and remote broker and handles messages flow
pub struct Bridge {
    system_address: String,
    device_id: String,
    connection_settings: ConnectionSettings,
    local_handler: MessageHandler,
    remote_handler: MessageHandler,
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
                    format!("{}{}", sub.remote(), sub.pattern().to_string()),
                )
            })
            .collect();

        let subs_map: HashMap<String, String> = connection_settings
            .subscriptions()
            .iter()
            .map(|sub| {
                (
                    format!("{}{}", sub.local().unwrap_or(""), sub.pattern().to_string()),
                    format!("{}{}", sub.remote(), sub.pattern().to_string()),
                )
            })
            .collect();

        Bridge {
            system_address,
            device_id,
            connection_settings,
            local_handler: MessageHandler::new(forwards_map),
            remote_handler: MessageHandler::new(subs_map),
        }
    }

    pub async fn start(&self) -> Result<(), Error> {
        info!("Starting bridge...{}", self.connection_settings.name());

        self.connect_to_local().await?;
        self.connect_to_remote().await?;

        Ok(())
    }

    async fn connect_to_remote(&self) -> Result<(), Error> {
        info!(
            "connecting to local broker {}",
            self.connection_settings.address()
        );

        let mut client =  MqttClient::new(
                self.system_address.as_str(),
                *self.connection_settings.keep_alive(),
                self.connection_settings.clean_session(),
                self.remote_handler.clone(),
                self.connection_settings.credentials());

        let subscriptions: Vec<_> = self
            .connection_settings
            .subscriptions()
            .iter()
            .map(|sub| format!("{}{}", sub.local().unwrap_or(""), sub.pattern().to_string()))
            .collect();
        debug!("subscribe to remote {:?}", subscriptions);
        // TODO: handle error
        client.subscribe(subscriptions).await?;

        // TODO: handle errors
        // TODO: use ref instead of clone
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
            self.local_handler.clone(),
            &Credentials::Anonymous(client_id),
        );

        let subscriptions: Vec<_> = self
            .connection_settings
            .forwards()
            .iter()
            .map(|sub| format!("{}{}", sub.local().unwrap_or(""), sub.pattern().to_string()))
            .collect();
        debug!("subscribe to local {:?}", subscriptions);
        // TODO: handle error
        client.subscribe(subscriptions).await?;

        // TODO: handle errors
        // TODO: use ref instead of clone
        client.handle_events().await?;

        Ok(())
    }
}

#[derive(Clone)]
pub struct MessageHandler {
    topics: HashMap<String, String>,
    inner: VecDeque<Publication>,
}

impl MessageHandler {
    pub fn new(topics: HashMap<String, String>) -> Self {
        Self {
            topics,
            inner: VecDeque::new(),
        }
    }

    // TODO: use TopicFilter to match topics and transform
    fn transform(&self, topic_name: &str) -> String {
        match self.topics.get(topic_name) {
            Some(topic) => topic.to_string(),
            None => "".to_string(),
        }
    }
}

#[async_trait]
impl EventHandler for MessageHandler {
    type Error = Error;

    // TODO: error
    async fn handle_event(&mut self, event: Event) -> Result<(), Error> {
        if let Event::Publication(publication) = event {
            // TODO: from received publication to publication
            self.inner.push_back(Publication {
                topic_name: self.transform(publication.topic_name.as_str()),
                qos: publication.qos,
                retain: publication.retain,
                payload: publication.payload,
            });
            debug!("Save message to store");
        }

        Ok(())
    }
}
