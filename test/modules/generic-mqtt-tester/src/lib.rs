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
    clippy::missing_errors_doc,
    clippy::missing_panics_doc
)]

use std::{
    env::VarError,
    fmt,
    io::{self},
};

use bytes::Buf;
use mqtt3::{PublishError, ReceivedPublication, UpdateSubscriptionError};
use tokio::{sync::mpsc::error::SendError, sync::mpsc::Sender, task::JoinError};

pub mod message_channel;
pub mod message_initiator;
pub mod settings;
pub mod tester;

pub const INITIATE_TOPIC_PREFIX: &str = "initiate";
pub const RELAY_TOPIC_PREFIX: &str = "relay";

#[derive(Debug, Clone)]
pub struct ShutdownHandle(Sender<()>);

impl ShutdownHandle {
    pub fn new(sender: Sender<()>) -> Self {
        Self(sender)
    }

    pub async fn shutdown(self) -> Result<(), MessageTesterError> {
        self.0
            .send(())
            .await
            .map_err(MessageTesterError::SendShutdownSignal)
    }
}

/// Used as an indicator of work that has finished. Needed to indicate that we
/// should not shutdown the thread corresponding to this work, as it has already
/// finished.
#[derive(Debug)]
pub enum ExitedWork {
    MessageChannel,
    MessageInitiator,
    PollClient,
    NoneOrUnknown, // Used for shutting down everything (None) or errored tasks (Unknown)
}

impl fmt::Display for ExitedWork {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        fmt::Debug::fmt(self, f)
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

    #[error("received rejected subscription: {0}")]
    RejectedSubscription(String),

    #[error("expected settings to contain a batch id")]
    MissingBatchId,

    #[error("expected settings to contain a tracking id")]
    MissingTrackingId,

    #[error("topic_suffix env var is needed to generate publish/subscribe topics")]
    TopicSuffixNeeded,
}

pub fn parse_sequence_number(publication: &ReceivedPublication) -> u32 {
    publication.payload.slice(0..4).get_u32()
}
