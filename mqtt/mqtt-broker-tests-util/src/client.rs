#![allow(clippy::mut_mut)]
use std::time::Duration;

use bytes::Bytes;
use futures::{future::FutureExt, select, stream::StreamExt};
use tokio::{
    net::ToSocketAddrs,
    sync::{
        mpsc::{self, UnboundedReceiver},
        oneshot::{self, Sender},
    },
    task::JoinHandle,
};

use mqtt3::{
    proto::{ClientId, Publication, QoS, SubscribeTo},
    Client, Event, PublishError, PublishHandle, ReceivedPublication, ShutdownHandle,
    UpdateSubscriptionHandle,
};

/// A wrapper on the [`mqtt3::Client`] to help simplify client event loop management.
#[derive(Debug)]
pub struct TestClient {
    publish_handle: PublishHandle,
    subscription_handle: UpdateSubscriptionHandle,

    /// Used for proper shutdown w/ Disconnect packet.
    shutdown_handle: ShutdownHandle,

    /// Used to simulate unexpected shutdown.
    termination_handle: Sender<()>,
    pub_receiver: UnboundedReceiver<ReceivedPublication>,
    sub_receiver: UnboundedReceiver<Event>,
    conn_receiver: UnboundedReceiver<Event>,
    event_loop_handle: JoinHandle<()>,
}

#[allow(dead_code)]
impl TestClient {
    pub async fn publish(&mut self, publication: Publication) -> Result<(), PublishError> {
        self.publish_handle.publish(publication).await
    }

    pub async fn publish_qos0(
        &mut self,
        topic: impl Into<String>,
        payload: impl Into<Bytes>,
        retain: bool,
    ) {
        self.publish(Publication {
            topic_name: topic.into(),
            qos: QoS::AtMostOnce,
            retain,
            payload: payload.into(),
        })
        .await
        .expect("couldn't publish")
    }

    pub async fn publish_qos1(
        &mut self,
        topic: impl Into<String>,
        payload: impl Into<Bytes>,
        retain: bool,
    ) {
        self.publish(Publication {
            topic_name: topic.into(),
            qos: QoS::AtLeastOnce,
            retain,
            payload: payload.into(),
        })
        .await
        .expect("couldn't publish")
    }

    pub async fn publish_qos2(
        &mut self,
        topic: impl Into<String>,
        payload: impl Into<Bytes>,
        retain: bool,
    ) {
        self.publish(Publication {
            topic_name: topic.into(),
            qos: QoS::ExactlyOnce,
            retain,
            payload: payload.into(),
        })
        .await
        .expect("couldn't publish")
    }

    pub async fn subscribe(&mut self, topic_filter: impl Into<String>, qos: QoS) {
        self.subscription_handle
            .subscribe(SubscribeTo {
                topic_filter: topic_filter.into(),
                qos,
            })
            .await
            .expect("couldn't subscribe to a topic")
    }

    /// Send the Disconnect packet and shutdown the client properly.
    pub async fn shutdown(mut self) {
        self.shutdown_handle
            .shutdown()
            .await
            .expect("couldn't shutdown");
        self.event_loop_handle
            .await
            .expect("couldn't terminate a client");
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

    pub fn connections(&mut self) -> &mut UnboundedReceiver<Event> {
        &mut self.conn_receiver
    }

    pub fn publications(&mut self) -> &mut UnboundedReceiver<ReceivedPublication> {
        &mut self.pub_receiver
    }

    pub fn subscriptions(&mut self) -> &mut UnboundedReceiver<Event> {
        &mut self.sub_receiver
    }
}

pub struct TestClientBuilder<T> {
    address: T,
    client_id: ClientId,
    username: Option<String>,
    password: Option<String>,
    will: Option<Publication>,
    max_reconnect_back_off: Duration,
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
            client_id: ClientId::ServerGenerated,
            username: None,
            password: None,
            will: None,
            max_reconnect_back_off: Duration::from_secs(1),
            keep_alive: Duration::from_secs(60),
        }
    }

    pub fn with_client_id(mut self, client_id: ClientId) -> Self {
        self.client_id = client_id;
        self
    }

    pub fn with_username(mut self, username: &str) -> Self {
        self.username = Some(username.into());
        self
    }

    pub fn with_password(mut self, password: &str) -> Self {
        self.password = Some(password.into());
        self
    }

    pub fn with_will(mut self, will: Publication) -> Self {
        self.will = Some(will);
        self
    }

    pub fn with_keep_alive(mut self, keep_alive: Duration) -> Self {
        self.keep_alive = keep_alive;
        self
    }

    pub fn build(self) -> TestClient {
        let address = self.address;
        let password = self.password;

        let io_source = move || {
            let address = address.clone();
            let password = password.clone();
            Box::pin(async move {
                let io = tokio::net::TcpStream::connect(address).await;
                io.map(|io| (io, password))
            })
        };

        let client = match self.client_id {
            ClientId::IdWithCleanSession(client_id) => Client::new(
                Some(client_id),
                self.username,
                self.will,
                io_source,
                self.max_reconnect_back_off,
                self.keep_alive,
            ),
            ClientId::IdWithExistingSession(client_id) => Client::from_state(
                client_id,
                self.username,
                self.will,
                io_source,
                self.max_reconnect_back_off,
                self.keep_alive,
            ),
            ClientId::ServerGenerated => Client::new(
                None,
                self.username,
                self.will,
                io_source,
                self.max_reconnect_back_off,
                self.keep_alive,
            ),
        };

        let publish_handle = client
            .publish_handle()
            .expect("couldn't get publish handle");

        let subscription_handle = client
            .update_subscription_handle()
            .expect("couldn't get subscribe handle");

        let shutdown_handle = client
            .shutdown_handle()
            .expect("couldn't get shutdown handle");

        let (pub_sender, pub_receiver) = mpsc::unbounded_channel();
        let (sub_sender, sub_receiver) = mpsc::unbounded_channel();
        let (conn_sender, conn_receiver) = mpsc::unbounded_channel();

        let (termination_handle, shutdown_rx) = oneshot::channel::<()>();

        let event_loop_handle = tokio::spawn(async move {
            let mut shutdown_rx = shutdown_rx.fuse();
            let mut client = client.fuse();

            loop {
                select! {
                    _ = shutdown_rx => {
                        return;
                    }
                    event = client.next() => {
                        match event {
                            Some(event) => {
                                let event = event.expect("got error instead of event");
                                match event {
                                    Event::NewConnection { .. } => conn_sender
                                        .send(event)
                                        .expect("can't send an event to a conn channel"),
                                    Event::Publication(publication) => pub_sender
                                        .send(publication)
                                        .expect("can't send an event to a pub channel"),
                                    Event::SubscriptionUpdates(_) => sub_sender
                                        .send(event)
                                        .expect("can't send an event to a sub channel"),
                                }
                            }
                            None => {
                                return;
                            }
                        }
                    }
                };
            }
        });

        TestClient {
            publish_handle,
            subscription_handle,
            shutdown_handle,
            termination_handle,
            pub_receiver,
            sub_receiver,
            conn_receiver,
            event_loop_handle,
        }
    }
}
