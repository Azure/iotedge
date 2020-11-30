///! This module contains tests that verify non-MQTT related functionality
///! of the broker (like config, storage, cleanup, etc...).
///!
///! For tests related to MQTT protocol please use `compliance.rs`.
use std::{any::Any, convert::Infallible, time::Duration};

use chrono::Utc;
use mqtt3::{proto::ClientId, ConnectionError, Event};
use mqtt_broker::{
    auth::AllowAll,
    auth::{Activity, Authorization, Authorizer},
    BrokerBuilder, BrokerSnapshot, Message, SystemEvent,
};
use mqtt_broker_tests_util::{
    client::TestClientBuilder,
    server::{start_server, DummyAuthenticator},
};

/// Validates the case when offline session is dropped if expired.
#[tokio::test]
async fn drop_session_on_expiry() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let broker_handle = broker.handle();
    let mut server_handle = start_server(broker, DummyAuthenticator::with_id("device_1"));

    // connect a client with persistent session.
    let mut offline_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("offline_client".into()))
        .build();

    // connect another client with persistent session that is always connected.
    let mut online_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("online_client".into()))
        .build();

    // assert clients connected
    assert_eq!(
        offline_client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );
    assert_eq!(
        online_client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );

    // disconnect client and bring its session to offline state.
    offline_client.shutdown().await;

    // let broker process disconnect.
    tokio::time::delay_for(Duration::from_secs(1)).await;

    // send cleanup signal that should remove the offline session.
    let expiration = Utc::now();
    broker_handle
        .send(Message::System(SystemEvent::SessionCleanup(expiration)))
        .expect("unable to send cleanup signal");

    // assert that offline session is removed.
    // and only "online" client's session remains.
    let (_, mut sessions) = server_handle.shutdown().await.into_parts();
    assert_eq!(1, sessions.len());

    let (client_info, _, _, _) = sessions.remove(0).into_parts();
    assert_eq!("online_client", client_info.client_id().as_str());

    // dispose a client.
    online_client.shutdown().await;
}

/// Validates the case when offline session is dropped if expired
/// even if broker restarted.
#[tokio::test]
async fn drop_session_on_expiry_after_restart() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let mut server_handle = start_server(broker, DummyAuthenticator::with_id("device_1"));

    // connect a client with persistent session.
    let mut offline_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("offline_client".into()))
        .build();

    // connect another client with persistent session that is always connected.
    let mut online_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("online_client".into()))
        .build();

    // assert clients connected
    assert_eq!(
        offline_client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );
    assert_eq!(
        online_client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );

    // disconnect client and bring its session to offline state.
    offline_client.shutdown().await;

    // let broker process disconnect.
    tokio::time::delay_for(Duration::from_secs(1)).await;

    // take a expiration date now, so sessions created above would be
    // considered expired.
    let expiration = Utc::now();

    // restart the broker.
    let (retained, sessions) = server_handle.shutdown().await.into_parts();
    assert_eq!(2, sessions.len());

    let broker = BrokerBuilder::default()
        .with_authorizer(AllowAll)
        .with_state(BrokerSnapshot::new(retained, sessions))
        .build();

    let broker_handle = broker.handle();
    let mut server_handle = start_server(broker, DummyAuthenticator::with_id("device_1"));

    // connect client with the same id and persistent session.
    let mut online_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("online_client".into()))
        .build();

    // assert client reconnected with persistent session.
    assert_eq!(
        online_client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: false
        })
    );

    // send cleanup signal that should remove the offline session.
    broker_handle
        .send(Message::System(SystemEvent::SessionCleanup(expiration)))
        .expect("unable to send cleanup signal");

    // assert that offline session is removed.
    // and only "online" client's session remains.
    let (_, mut sessions) = server_handle.shutdown().await.into_parts();
    assert_eq!(1, sessions.len());

    let (client_info, _, _, _) = sessions.remove(0).into_parts();
    assert_eq!("online_client", client_info.client_id().as_str());

    // dispose a client.
    online_client.shutdown().await;
}

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
    let mut root_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("root".into()))
        .build();

    // assert clients connected
    assert_eq!(
        client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );
    assert_eq!(
        root_client.connections().recv().await,
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

    let (client_info, _, _, _) = sessions.remove(0).into_parts();
    assert_eq!("root", client_info.client_id().as_str());

    // dispose a client.
    client.shutdown().await;
    root_client.shutdown().await;
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
