#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(unused_variables, dead_code)] // TODO: remove when module complete
use std::env::VarError;

use mqtt3::{PublishError, ReceivedPublication, UpdateSubscriptionError};
use tokio::{sync::mpsc::error::SendError, task::JoinError};

pub mod message_channel;
pub mod settings;
pub mod tester;

pub const BACKWARDS_TOPIC: &str = "backwards/1";
pub const FORWARDS_TOPIC: &str = "forwards/1";

#[derive(Debug, thiserror::Error)]
pub enum MessageTesterError {
    #[error("could not parse expected env vars")]
    ParseEnvironment(#[source] VarError),

    #[error("could not get client publish handle")]
    PublishHandle(#[source] PublishError),

    #[error("failed to publish")]
    Publish(#[source] PublishError),

    #[error("could not send shutdown signal")]
    SendShutdownSignal(#[source] SendError<()>),

    #[error("failure listening for shutdown")]
    ListenForShutdown,

    #[error("failure listening for incoming publications")]
    ListenForIncomingPublications,

    #[error("thread panicked while waiting for shutdown")]
    WaitForShutdown(#[source] JoinError),

    #[error("could not send publication to message handler")]
    SendPublicationInChannel(#[source] SendError<ReceivedPublication>),

    #[error("failure getting client subscription handle")]
    UpdateSubscriptionHandle(#[source] UpdateSubscriptionError),

    #[error("failure making client subscriptions")]
    UpdateSubscription(#[source] UpdateSubscriptionError),
}
