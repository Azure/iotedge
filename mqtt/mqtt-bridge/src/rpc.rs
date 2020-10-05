use async_trait::async_trait;

use bson::Document;
use bytes::buf::BufExt;
use mqtt3::Event;
use serde::{Deserialize, Serialize};

use crate::client::EventHandler;

pub struct RpcHandler {}

impl RpcHandler {
    pub fn new() -> Self {
        Self {}
    }

    async fn handle_command(&self, command: RpcCommand) {
        match command {
            RpcCommand::Subscribe { topic_filter } => {}
            RpcCommand::Unsubscribe { topic_filter } => {}
            RpcCommand::Publish { topic, payload } => {}
        }
    }
}

#[async_trait]
impl EventHandler for RpcHandler {
    type Error = bson::de::Error;

    async fn handle(&mut self, event: Event) -> Result<(), Self::Error> {
        if let Event::Publication(publication) = event {
            let doc = Document::from_reader(&mut publication.payload.reader())?;
            match bson::from_document(doc)? {
                VersionedRpcCommand::V1(command) => self.handle_command(command).await,
            }
        }

        Ok(())
    }
}

#[derive(Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", tag = "cmd")]
enum RpcCommand {
    #[serde(rename = "sub")]
    Subscribe {
        #[serde(rename = "topic")]
        topic_filter: String,
    },

    #[serde(rename = "unsub")]
    Unsubscribe {
        #[serde(rename = "topic")]
        topic_filter: String,
    },

    #[serde(rename = "pub")]
    Publish {
        topic: String,

        #[serde(with = "serde_bytes")]
        payload: Vec<u8>,
    },
}

#[derive(Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", tag = "version")]
enum VersionedRpcCommand {
    V1(RpcCommand),
}

#[cfg(test)]
mod tests {
    use bson::bson;

    use super::*;

    #[test]
    fn it_deserizes_from_bson() {
        let commands = vec![
            (
                bson!({
                    "version": "v1",
                    "cmd": "sub",
                    "topic": "/foo",
                }),
                VersionedRpcCommand::V1(RpcCommand::Subscribe {
                    topic_filter: "/foo".into(),
                }),
            ),
            (
                bson!({
                    "version": "v1",
                    "cmd": "unsub",
                    "topic": "/foo",
                }),
                VersionedRpcCommand::V1(RpcCommand::Unsubscribe {
                    topic_filter: "/foo".into(),
                }),
            ),
            (
                bson!({
                    "version": "v1",
                    "cmd": "pub",
                    "topic": "/foo",
                    "payload": vec![100, 97, 116, 97]
                }),
                VersionedRpcCommand::V1(RpcCommand::Publish {
                    topic: "/foo".into(),
                    payload: b"data".to_vec(),
                }),
            ),
        ];

        for (command, expected) in commands {
            let rpc: VersionedRpcCommand = bson::from_bson(command).unwrap();
            assert_eq!(rpc, expected);
        }
    }
}
