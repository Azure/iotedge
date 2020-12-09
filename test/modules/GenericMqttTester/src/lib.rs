#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(unused_variables, dead_code)] // TODO: remove when module complete
use std::env::VarError;

use mqtt3::PublishError;
use tokio::sync::mpsc::{error::SendError, Sender};

pub mod message_handler;
pub mod settings;
pub mod tester;

#[derive(Debug, thiserror::Error)]
pub enum MessageTesterError {
    #[error("could not parse expected module environment vars")]
    ParseEnvironment(#[source] VarError),

    #[error("could not get client publish handle")]
    PublishHandle(#[source] PublishError),

    #[error("failed to publish")]
    Publish(#[source] PublishError),

    #[error("could not send signal to shutdown message handler")]
    ShutdownMessageHandler(#[source] SendError<()>),
}

#[derive(Debug, Clone)]
pub struct ShutdownHandle(Sender<()>);

impl ShutdownHandle {
    pub fn new(sender: Sender<()>) -> Self {
        Self(sender)
    }

    pub async fn shutdown(mut self) -> Result<(), MessageTesterError> {
        self.0
            .send(())
            .await
            .map_err(MessageTesterError::ShutdownMessageHandler)
    }
}
