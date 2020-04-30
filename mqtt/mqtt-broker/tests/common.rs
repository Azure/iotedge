use std::{
    pin::Pin,
    sync::atomic::{AtomicU32, Ordering},
    task::{Context, Poll},
    time::Duration,
};

use futures::{future::select, pin_mut};
use futures_util::{FutureExt, StreamExt};
use lazy_static::lazy_static;
use tokio::{
    net::ToSocketAddrs,
    stream::Stream,
    sync::{
        mpsc::{self, UnboundedReceiver},
        oneshot::{self, Sender},
    },
    task::JoinHandle,
};

use mqtt3::{
    proto::{Publication, QoS, SubscribeTo},
    Client, Event, PublishError, PublishHandle, ShutdownHandle, UpdateSubscriptionError,
    UpdateSubscriptionHandle,
};
use mqtt_broker::{Authenticator, Authorizer, Broker, BrokerState, Error, Server};

/// A wrapper on the [`mqtt3::Client`] to help simplify client event loop management.
#[derive(Debug)]
pub struct TestClient {
    publish_handle: PublishHandle,
    subscription_handle: UpdateSubscriptionHandle,

    /// Used for proper shutdown w/ Disconnect packet.
    shutdown_handle: ShutdownHandle,

    /// Used to simulate unexpected shutdown.
    termination_handle: Sender<()>,
    events_receiver: UnboundedReceiver<Event>,
    event_loop_handle: JoinHandle<()>,
}

impl TestClient {
    pub async fn publish(&mut self, publication: Publication) -> Result<(), PublishError> {
        self.publish_handle.publish(publication).await
    }

    pub async fn publish_qos0(&mut self, topic: &str, payload: &str, retain: bool) {
        self.publish(Publication {
            topic_name: topic.into(),
            qos: QoS::AtMostOnce,
            retain,
            payload: payload.to_owned().into(),
        })
        .await
        .expect("couldn't publish")
    }

    pub async fn publish_qos1(&mut self, topic: &str, payload: &str, retain: bool) {
        self.publish(Publication {
            topic_name: topic.into(),
            qos: QoS::AtLeastOnce,
            retain,
            payload: payload.to_owned().into(),
        })
        .await
        .expect("couldn't publish")
    }

    pub async fn publish_qos2(&mut self, topic: &str, payload: &str, retain: bool) {
        self.publish(Publication {
            topic_name: topic.into(),
            qos: QoS::ExactlyOnce,
            retain,
            payload: payload.to_owned().into(),
        })
        .await
        .expect("couldn't publish")
    }

    pub async fn subscribe(
        &mut self,
        subscribe_to: SubscribeTo,
    ) -> Result<(), UpdateSubscriptionError> {
        self.subscription_handle.subscribe(subscribe_to).await
    }

    pub async fn subscribe_qos2(&mut self, topic_filter: &str) {
        self.subscribe(SubscribeTo {
            topic_filter: topic_filter.into(),
            qos: QoS::ExactlyOnce,
        })
        .await
        .expect("couldn't subscribe to a topic")
    }

    /// Initiates sending Disconnect packet and proper client shutdown.
    pub async fn shutdown(&mut self) {
        self.shutdown_handle
            .shutdown()
            .await
            .expect("couldn't shutdown")
    }

    pub fn shutdown_handle(&mut self) -> ShutdownHandle {
        self.shutdown_handle.clone()
    }

    /// Terminates client w/o sending Disconnect packet.
    pub async fn terminate(self) {
        self.termination_handle
            .send(())
            .expect("unable to send termination signal");
        self.event_loop_handle
            .await
            .expect("couldn't terminate a client")
    }

    /// Waits until client's event loop is finished.
    pub async fn join(self) {
        self.event_loop_handle
            .await
            .expect("couldn't wait for client event loop to finish")
    }

    pub async fn try_recv(&mut self) -> Option<Event> {
        self.events_receiver.try_recv().ok()
    }
}

impl Stream for TestClient
where
    Self: Unpin,
{
    type Item = Event;

    fn poll_next(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        self.events_receiver.poll_recv(cx)
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
    keep_alive: Duration,
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
            keep_alive: Duration::from_secs(60),
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

    pub fn keep_alive(mut self, keep_alive: Duration) -> Self {
        self.keep_alive = keep_alive;
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
            self.keep_alive,
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

        let (events_sender, events_receiver) = mpsc::unbounded_channel();

        let (termination_handle, tx) = oneshot::channel::<()>();

        let event_loop_handle = tokio::spawn(async move {
            let event_loop = async {
                while let Some(event) = client.next().await {
                    let event = event.expect("event expected");
                    events_sender
                        .send(event)
                        .expect("can't send an event to a channel");
                }
            };
            pin_mut!(event_loop);
            select(event_loop, tx).await;
        });

        TestClient {
            publish_handle,
            subscription_handle,
            shutdown_handle,
            termination_handle,
            events_receiver,
            event_loop_handle,
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
