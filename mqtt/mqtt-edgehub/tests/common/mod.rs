use std::{any::Any, error::Error as StdError};

use futures_util::StreamExt;
use mqtt_broker_tests_util::client::TestClientBuilder;
use tokio::task::JoinHandle;

use mqtt3::{
    proto::{ClientId, QoS},
    ShutdownError,
};
use mqtt_broker::{
    auth::{Activity, Authorization, Authorizer},
    sidecar::Sidecar,
};
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
    let mut command_handler = Box::new(CommandHandler::new(system_address, "test-device"));
    command_handler.add_command(command);

    let shutdown_handle: ShutdownHandle = command_handler.shutdown_handle().unwrap();

    let join_handle = tokio::spawn(command_handler.run());

    Ok((shutdown_handle, join_handle))
}

pub async fn start_command_handler_and_wait_ready<C, E>(
    system_address: String,
    command: C,
) -> Result<(ShutdownHandle, JoinHandle<()>), ShutdownError>
where
    C: Command<Error = E> + Send + 'static,
    E: StdError + 'static,
{
    let mut readiness_client = TestClientBuilder::new(system_address.clone())
        .with_client_id(ClientId::IdWithCleanSession("$edgehub".into()))
        .build();

    readiness_client
        .subscribe("$edgehub/#", QoS::AtLeastOnce)
        .await;

    let requested_topic = command.topic().to_owned();

    let handles = start_command_handler(system_address, command).await?;

    while let Some(publication) = readiness_client.publications().next().await {
        if publication.topic_name == "$edgehub/test-device/$edgeHub/$broker/subscriptions" {
            if let Ok(subscriptions) =
                serde_json::from_slice::<Vec<String>>(&publication.payload[..])
            {
                if subscriptions.contains(&requested_topic) {
                    break;
                }
            }
        }
    }

    Ok(handles)
}
