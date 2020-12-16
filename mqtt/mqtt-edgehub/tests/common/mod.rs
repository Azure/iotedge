#![allow(dead_code)]
use std::{any::Any, error::Error as StdError};

use tokio::{
    sync::mpsc::{self, UnboundedReceiver, UnboundedSender},
    task::JoinHandle,
};

use mqtt3::ShutdownError;
use mqtt_broker::{
    auth::{Activity, Authorization, Authorizer},
    sidecar::Sidecar,
};
use mqtt_edgehub::command::{Command, CommandHandler, ShutdownHandle};

pub const LOCAL_BROKER_SUFFIX: &str = "$edgeHub/$broker";

// We need a `DummyAuthorizer` to authorize the command handler and $edgehub.
//
// LocalAuthorizer currently wraps `EdgeHubAuthorizer` and `PolicyAuthorizer` in production code,
// but LocalAuthorizer would authorize everything in the case of an integ test.
//
// In addition, `DummyAuthorizer` provides a way to signal when authorizer receives updates.
pub struct DummyAuthorizer<Z> {
    inner: Z,
    receiver: Option<UnboundedReceiver<()>>,
    sender: UnboundedSender<()>,
}

impl<Z> DummyAuthorizer<Z>
where
    Z: Authorizer,
{
    pub fn new(inner: Z) -> Self {
        let (sender, receiver) = mpsc::unbounded_channel();
        Self {
            inner,
            receiver: Some(receiver),
            sender,
        }
    }

    /// A receiver that signals when authorizer update has happened.
    pub fn update_signal(&mut self) -> UnboundedReceiver<()> {
        self.receiver
            .take()
            .expect("You can get only one receiver instance")
    }
}

impl<Z, E> Authorizer for DummyAuthorizer<Z>
where
    Z: Authorizer<Error = E>,
    E: StdError,
{
    type Error = E;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        if activity
            .client_id()
            .to_string()
            .contains(LOCAL_BROKER_SUFFIX)
            || activity.client_id().as_str() == "$edgehub"
        {
            Ok(Authorization::Allowed)
        } else {
            self.inner.authorize(activity)
        }
    }

    fn update(&mut self, update: Box<dyn Any>) -> Result<(), Self::Error> {
        self.inner.update(update)?;
        self.sender.send(()).expect("unable to send update signal");
        Ok(())
    }
}

pub async fn start_command_handler<C, E>(
    system_address: String,
    command: C,
) -> Result<(ShutdownHandle, JoinHandle<()>), ShutdownError>
where
    C: Command<Error = E> + Send + 'static,
    E: StdError + 'static,
{
    let mut command_handler = Box::new(CommandHandler::new(system_address, "test-device"));
    command_handler.add_command(command);

    let shutdown_handle: ShutdownHandle = command_handler.shutdown_handle().unwrap();

    let join_handle = tokio::spawn(command_handler.run());

    Ok((shutdown_handle, join_handle))
}
