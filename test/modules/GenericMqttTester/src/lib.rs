#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::{
    env::VarError,
    io::{self},
};

use mqtt3::{PublishError, ReceivedPublication, UpdateSubscriptionError};
use tokio::{sync::mpsc::error::SendError, sync::mpsc::Sender, task::JoinError};
use trc_client::ReportResultError;

pub mod message_channel;
pub mod message_initiator;
pub mod settings;
pub mod tester;

pub const BACKWARDS_TOPIC: &str = "backwards/1";
pub const FORWARDS_TOPIC: &str = "forwards/1";
const SEND_SOURCE: &str = "genericMqttTester.send";
const RECEIVE_SOURCE: &str = "genericMqttTester.receive";

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
            .map_err(MessageTesterError::SendShutdownSignal)
    }
}

#[derive(Debug, thiserror::Error)]
pub enum MessageTesterError {
    #[error("could not parse expected env vars")]
    ParseEnvironment(#[from] VarError),

    #[error("could not get client publish handle")]
    PublishHandle(#[source] PublishError),

    #[error("failed to publish")]
    Publish(#[source] PublishError),

    #[error("could not send shutdown signal")]
    SendShutdownSignal(#[from] SendError<()>),

    #[error("failure listening for shutdown")]
    ListenForShutdown,

    #[error("failure listening for incoming publications")]
    ListenForIncomingPublications,

    #[error("thread panicked while waiting for shutdown")]
    WaitForShutdown(#[from] JoinError),

    #[error("could not send publication to message handler")]
    SendPublicationInChannel(#[from] SendError<ReceivedPublication>),

    #[error("failure getting client subscription handle")]
    UpdateSubscriptionHandle(#[source] UpdateSubscriptionError),

    #[error("failure making client subscriptions")]
    UpdateSubscription(#[source] UpdateSubscriptionError),

    #[error("failure creating stream to listen for unix signal")]
    CreateUnixSignalListener(#[from] io::Error),

    #[error("received unexpected value from unix signal listener")]
    ListenForUnixSignal,

    #[error("failed to parse sequence number from publication")]
    DeserializeMessage(#[from] serde_json::Error),

    #[error("failed to report test result")]
    ReportResult(#[from] ReportResultError),

    #[error("received rejected subscription: {0}")]
    RejectedSubscription(String),
}
