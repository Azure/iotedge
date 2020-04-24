use std::{
    sync::atomic::{AtomicU32, Ordering},
    time::Duration,
};

use futures_util::{FutureExt, StreamExt};
use lazy_static::lazy_static;
use tokio::net::ToSocketAddrs;
use tokio::sync::{
    mpsc::{self, Receiver},
    oneshot::{self, Sender},
};
use tokio::task::{JoinError, JoinHandle};

use mqtt3::{
    proto::{Publication, SubscribeTo},
    Client, Event, PublishError, PublishHandle, ShutdownError, ShutdownHandle,
    UpdateSubscriptionError, UpdateSubscriptionHandle,
};
use mqtt_broker::{Authenticator, Authorizer, Broker, BrokerState, Error, Server};

/// A wrapper on the [`mqtt3::Client`] to help simplify client event loop management.
#[derive(Debug)]
pub struct TestClient {
    publish_handle: PublishHandle,
    subscription_handle: UpdateSubscriptionHandle,
    shutdown_handle: ShutdownHandle,
    pub events_receiver: Receiver<Event>,
    task: JoinHandle<()>,
}

impl TestClient {
    pub async fn publish(&mut self, publication: Publication) -> Result<(), PublishError> {
        self.publish_handle.publish(publication).await
    }

    pub async fn subscribe(
        &mut self,
        subscribe_to: SubscribeTo,
    ) -> Result<(), UpdateSubscriptionError> {
        self.subscription_handle.subscribe(subscribe_to).await
    }

    pub async fn shutdown(&mut self) -> Result<(), ShutdownError> {
        self.shutdown_handle.shutdown().await
    }

    pub async fn join(self) -> Result<(), JoinError> {
        self.task.await
    }
}

pub struct TestClientBuilder<T>
where
    T: ToSocketAddrs + Clone + Send + Sync + Unpin + 'static,
{
    address: T,
    client_id: Option<String>,
    username: Option<String>,
    password: Option<String>,
    will: Option<Publication>,
}

#[allow(dead_code)]
impl<T> TestClientBuilder<T>
where
    T: ToSocketAddrs + Clone + Send + Sync + Unpin + 'static,
{
    pub fn new(address: T) -> Self {
        Self {
            address,
            client_id: None,
            username: None,
            password: None,
            will: None,
        }
    }

    pub fn client_id(mut self, client_id: &str) -> Self {
        self.client_id = Some(client_id.into());
        self
    }

    pub fn username(mut self, username: &str) -> Self {
        self.username = Some(username.into());
        self
    }

    pub fn password(mut self, password: &str) -> Self {
        self.password = Some(password.into());
        self
    }

    pub fn will(mut self, will: Publication) -> Self {
        self.will = Some(will);
        self
    }

    pub fn build(self) -> TestClient {
        let address = self.address;
        let password = self.password;

        let mut client = Client::new(
            self.client_id,
            self.username,
            self.will,
            move || {
                let address = address.clone();
                let password = password.clone();
                Box::pin(async move {
                    let io = tokio::net::TcpStream::connect(&address).await;
                    io.map(|io| (io, password))
                })
            },
            Duration::from_secs(1),
            Duration::from_secs(60),
        );

        let publish_handle = client
            .publish_handle()
            .expect("couldn't get publish handle");

        let subscription_handle = client
            .update_subscription_handle()
            .expect("couldn't get subscribe handle");

        let shutdown_handle = client
            .shutdown_handle()
            .expect("couldn't get shutdown handle");

        let (mut events_sender, events_receiver) = mpsc::channel(128);

        let task = tokio::spawn(async move {
            while let Some(event) = client.next().await {
                let event = event.expect("event expected");
                events_sender
                    .send(event)
                    .await
                    .expect("can't send an event to a channel");
            }
        });

        TestClient {
            publish_handle,
            subscription_handle,
            shutdown_handle,
            events_receiver,
            task,
        }
    }
}

/// Starts a test server with a provided broker and returns
/// shutdown handle, broker task and server binding.
pub fn start_server<N, Z>(
    broker: Broker<N, Z>,
) -> (Sender<()>, JoinHandle<Result<BrokerState, Error>>, String)
where
    N: Authenticator + Send + Sync + 'static,
    Z: Authorizer + Send + Sync + 'static,
{
    lazy_static! {
        static ref PORT: AtomicU32 = AtomicU32::new(5555);
    }

    let port = PORT.fetch_add(1, Ordering::SeqCst);
    let address: String = format!("localhost:{}", port);

    let (shutdown, rx) = oneshot::channel::<()>();
    let transports = vec![mqtt_broker::TransportBuilder::Tcp(address.clone())];
    let broker_task = tokio::spawn(Server::from_broker(broker).serve(transports, rx.map(drop)));

    (shutdown, broker_task, address)
}
