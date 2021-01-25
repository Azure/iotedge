#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms,
    clippy::must_use_candidate,
    clippy::missing_errors_doc
)]

use std::{
    env::VarError,
    io::{self},
    num::ParseIntError,
};

use mqtt3::{PublishError, ReceivedPublication, UpdateSubscriptionError};
use tokio::{sync::mpsc::error::SendError, sync::mpsc::Sender, task::JoinError};
use trc_client::ReportResultError;

pub mod message_channel;
pub mod message_initiator;
pub mod settings;
pub mod tester;

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
    #[error("could not parse expected env vars: {0:?}")]
    ParseEnvironment(#[from] VarError),

    #[error("could not get client publish handle: {0:?}")]
    PublishHandle(#[source] PublishError),

    #[error("failed to publish: {0:?}")]
    Publish(#[source] PublishError),

    #[error("could not send shutdown signal: {0:?}")]
    SendShutdownSignal(#[from] SendError<()>),

    #[error("failure listening for shutdown")]
    ListenForShutdown,

    #[error("failure listening for incoming publications")]
    ListenForIncomingPublications,

    #[error("thread panicked while waiting for shutdown: {0:?}")]
    WaitForShutdown(#[from] JoinError),

    #[error("could not send publication to message handler: {0:?}")]
    SendPublicationInChannel(#[from] SendError<ReceivedPublication>),

    #[error("failure getting client subscription handle: {0:?}")]
    UpdateSubscriptionHandle(#[source] UpdateSubscriptionError),

    #[error("failure making client subscriptions: {0:?}")]
    UpdateSubscription(#[source] UpdateSubscriptionError),

    #[error("failure creating stream to listen for unix signal: {0:?}")]
    CreateUnixSignalListener(#[from] io::Error),

    #[error("received unexpected value from unix signal listener")]
    ListenForUnixSignal,

    #[error("failed to parse publication payload: {0:?}")]
    DeserializePayload(#[from] serde_json::Error),

    #[error("failed to deserialize sequence number from publication payload")]
    DeserializeSequenceNumber,

    #[error("failed to parse sequence number from publication payload: {0}")]
    ParseSequenceNumber(ParseIntError),

    #[error("failed to report test result: {0:?}")]
    ReportResult(#[from] ReportResultError),

    #[error("received rejected subscription: {0}")]
    RejectedSubscription(String),
}
