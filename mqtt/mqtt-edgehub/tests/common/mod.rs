use std::{any::Any, error::Error as StdError};

use tokio::task::JoinHandle;

use mqtt3::ShutdownError;
use mqtt_broker::auth::{Activity, Authorization, Authorizer};
use mqtt_edgehub::command::{Command, CommandHandler, ShutdownHandle};

pub const LOCAL_BROKER_SUFFIX: &str = "$edgeHub/$broker";

// We need a Dummy Authorizer to authorize the command handler and $edgehub
// LocalAuthorizer currently wraps EdgeHubAuthorizer in production code,
// but LocalAuthorizer would authorize everything in the case of an integ test.
pub struct DummyAuthorizer<Z>(Z);

impl<Z> DummyAuthorizer<Z>
where
    Z: Authorizer,
{
    #![allow(dead_code)]
    pub fn new(authorizer: Z) -> Self {
        Self(authorizer)
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
            self.0.authorize(activity)
        }
    }

    fn update(&mut self, update: Box<dyn Any>) -> Result<(), Self::Error> {
        self.0.update(update)
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
    let mut command_handler = CommandHandler::new(system_address, "test-device");
    command_handler.add_command(command);

    command_handler.init().await.unwrap();

    let shutdown_handle: ShutdownHandle = command_handler.shutdown_handle().unwrap();

    let join_handle = tokio::spawn(command_handler.run());

    Ok((shutdown_handle, join_handle))
}
