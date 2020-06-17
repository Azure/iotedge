use std::{
    sync::atomic::{AtomicU32, Ordering},
    task::{Context, Poll},
    time::{Duration, Instant},
};

use bytes::Bytes;
use futures::{future::select, pin_mut, Stream};
use futures_util::{sink::SinkExt, FutureExt, StreamExt};
use lazy_static::lazy_static;
use tokio::{
    net::{TcpStream, ToSocketAddrs},
    sync::{
        mpsc::{self, UnboundedReceiver},
        oneshot::{self, Sender},
    },
    task::JoinHandle,
};
use tokio_io_timeout::TimeoutStream;
use tokio_util::codec::Framed;

use mqtt3::{
    proto::{ClientId, Connect, Packet, PacketCodec, Publication, Publish, QoS, SubscribeTo},
    Client, Event, PublishError, PublishHandle, ReceivedPublication, ShutdownHandle,
    UpdateSubscriptionHandle, PROTOCOL_LEVEL, PROTOCOL_NAME,
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
    pub_receiver: UnboundedReceiver<ReceivedPublication>,
    sub_receiver: UnboundedReceiver<Event>,
    conn_receiver: UnboundedReceiver<Event>,
    event_loop_handle: JoinHandle<()>,
}

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

    pub fn client_id(mut self, client_id: ClientId) -> Self {
        self.client_id = client_id;
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

        let io_source = move || {
            let address = address.clone();
            let password = password.clone();
            Box::pin(async move {
                let io = tokio::net::TcpStream::connect(address).await;
                io.map(|io| (io, password))
            })
        };

        let mut client = match self.client_id {
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

        let (termination_handle, tx) = oneshot::channel::<()>();

        let event_loop_handle = tokio::spawn(async move {
            let event_loop = async {
                while let Some(event) = client.next().await {
                    let event = event.expect("event expected");
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
                    };
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
            pub_receiver,
            sub_receiver,
            conn_receiver,
            event_loop_handle,
        }
    }
}

lazy_static! {
    static ref DEFAULT_TIMEOUT: Duration = Duration::from_secs(5);
}

/// A simple wrapper around TcpStream + PacketCodec to send specific packets
/// to a broker for more granular integration testing.
#[derive(Debug)]
pub struct PacketStream {
    codec: Framed<TimeoutStream<TcpStream>, PacketCodec>,
}

impl PacketStream {
    /// Creates a client and opens TCP connection to the server.
    /// No MQTT packets are sent at this moment.
    pub async fn open(server_addr: impl ToSocketAddrs) -> Self {
        // broker may not be available immediately in the test,
        // so we'll try to connect for some time.
        let mut result = TcpStream::connect(&server_addr).await;
        let start_time = Instant::now();
        while let Err(_) = result {
            tokio::time::delay_for(Duration::from_millis(100)).await;
            if start_time.elapsed() > *DEFAULT_TIMEOUT {
                break;
            }
            result = TcpStream::connect(&server_addr).await;
        }

        let tcp_stream = result.expect("unable to establish tcp connection");
        let mut timeout = TimeoutStream::new(tcp_stream);
        timeout.set_read_timeout(Some(*DEFAULT_TIMEOUT));
        timeout.set_write_timeout(Some(*DEFAULT_TIMEOUT));

        let codec = Framed::new(timeout, PacketCodec::default());

        Self { codec }
    }

    /// Creates a client, opens TCP connection to the server,
    /// and sends CONNECT packet.
    pub async fn connect(
        client_id: ClientId,
        server_addr: impl ToSocketAddrs,
        username: Option<String>,
        password: Option<String>,
    ) -> Self {
        let mut client = Self::open(server_addr).await;
        client
            .send_connect(Connect {
                username,
                password,
                client_id,
                will: None,
                keep_alive: Duration::from_secs(30),
                protocol_name: PROTOCOL_NAME.into(),
                protocol_level: PROTOCOL_LEVEL,
            })
            .await;
        client
    }

    pub async fn send_connect(&mut self, connect: Connect) {
        self.send_packet(Packet::Connect(connect)).await;
    }

    pub async fn send_publish(&mut self, publish: Publish) {
        self.send_packet(Packet::Publish(publish)).await;
    }

    pub async fn send_packet(&mut self, packet: Packet) {
        self.codec
            .send(packet)
            .await
            .expect("Unable to send a packet");
    }
}

impl Stream for PacketStream {
    type Item = Packet;

    fn poll_next(
        mut self: std::pin::Pin<&mut Self>,
        cx: &mut Context<'_>,
    ) -> Poll<Option<Self::Item>> {
        match self.codec.poll_next_unpin(cx) {
            Poll::Ready(Some(result)) => {
                Poll::Ready(Some(result.expect("Error decoding incoming packet")))
            }
            Poll::Ready(None) => Poll::Ready(None),
            Poll::Pending => Poll::Pending,
        }
    }
}

/// Used to control server lifetime during tests. Implements
/// Drop to cleanup resources after every test.
pub struct ServerHandle {
    address: String,
    shutdown: Option<Sender<()>>,
    task: Option<JoinHandle<Result<BrokerState, Error>>>,
}

impl ServerHandle {
    pub fn address(&self) -> String {
        self.address.clone()
    }

    pub async fn shutdown(&mut self) -> BrokerState {
        self.shutdown
            .take()
            .unwrap()
            .send(())
            .expect("couldn't shutdown broker");

        self.task
            .take()
            .unwrap()
            .await
            .unwrap()
            .expect("can't wait for the broker")
    }
}

impl Drop for ServerHandle {
    fn drop(&mut self) {
        if let Some(sender) = self.shutdown.take() {
            sender.send(()).expect("couldn't shutdown broker");
        }
    }
}

/// Starts a test server with a provided broker and returns
/// shutdown handle, broker task and server binding.
pub fn start_server<N, Z>(broker: Broker<N, Z>) -> ServerHandle
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
    let task = tokio::spawn(Server::from_broker(broker).serve(transports, rx.map(drop)));

    ServerHandle {
        address,
        shutdown: Some(shutdown),
        task: Some(task),
    }
}
