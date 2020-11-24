///! This module contains tests that verify non-MQTT related functionality
///! of the broker (like config, storage, cleanup, etc...).
///!
///! For tests related to MQTT protocol please use `compliance.rs`.
use std::{any::Any, convert::Infallible};

use mqtt3::{proto::ClientId, ConnectionError, Event};
use mqtt_broker::{
    auth::{Activity, Authorization, Authorizer},
    BrokerBuilder, Message, SystemEvent,
};
use mqtt_broker_tests_util::{
    client::TestClientBuilder,
    server::{start_server, DummyAuthenticator},
};

/// Validates the case when authorization rules change leads to some
/// existing sessions (connected or offline) being unauthorized and dropped.
#[tokio::test]
async fn drop_sessions_on_reauthorize() {
    let broker = BrokerBuilder::default()
        .with_authorizer(BooleanAuthorizer(true))
        .build();

    let broker_handle = broker.handle();
    let mut server_handle = start_server(broker, DummyAuthenticator::with_id("device_1"));

    // connect a client with persistent session.
    let mut client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("device_1".into()))
        .build();

    // connect another client with persistent session that is always authorized.
    let root = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("root".into()))
        .build();

    // assert client connected
    assert_eq!(
        client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );

    broker_handle
        .send(Message::System(SystemEvent::AuthorizationUpdate(Box::new(
            false,
        ))))
        .expect("unable to send authorization update");

    // assert client disconnected.
    assert_eq!(
        client.connections().recv().await,
        Some(Event::Disconnected(ConnectionError::ServerClosedConnection))
    );

    // assert that unauthorized persisted session is removed.
    // and only "root" client's session remains.
    let (_, mut sessions) = server_handle.shutdown().await.into_parts();
    assert_eq!(1, sessions.len());

    let (client_info, _, _) = sessions.remove(0).into_parts();
    assert_eq!("root", client_info.client_id().as_str());

    // dispose a client.
    client.shutdown().await;
    root.shutdown().await;
}

struct BooleanAuthorizer(bool);

impl Authorizer for BooleanAuthorizer {
    type Error = Infallible;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        Ok(if self.0 || activity.client_id().as_str() == "root" {
            Authorization::Allowed
        } else {
            Authorization::Forbidden("not authorized".into())
        })
    }
    fn update(&mut self, update: Box<dyn Any>) -> Result<(), Self::Error> {
        self.0 = *update.downcast_ref::<bool>().expect("expected bool");
        Ok(())
    }
}
