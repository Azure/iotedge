//! `dispatch_message` benches start broker with empty state and no network connections.
//! Firstly, each bench test creates a given number of publishers, subscribers and connects
//! them to a broker. Then it makes a series of exercises to send messages to a broker and
//! measures the time between a message successfully sent to a broker and broker processed
//! this message. For this case broker extended with a `on_publish` "callback" which has to
//! be exposed only for benchmark tests.
//!
//! Scenarios supported:
//! * all subscribers receive messages on their own topic
//! * all subscribers receive messages on shared topic
//!
//! How to run benches
//! ```bash
//! cd mqtt
//! cargo bench --bench dispatch_messages \
//!   --features="benches" \
//!   --manifest-path mqtt-broker/Cargo.toml
//! ```
//! or
//! ```bash
//! cd mqtt/mqtt-broker
//! cargo bench --bench dispatch_messages --features="benches"
//! ```
//! to run all benches
//! ```bash
//! cd mqtt/mqtt-broker
//! cargo bench --all --features="benches"
//! ```

use std::{
    collections::HashSet,
    fmt::{Display, Formatter, Result as FmtResult},
    net::SocketAddr,
    sync::{Arc, Mutex},
    time::Duration,
};

use criterion::{
    criterion_group, criterion_main, measurement::WallTime, BenchmarkGroup, Criterion,
};
use itertools::iproduct;
use rand::Rng;
use tokio::{
    runtime::Runtime,
    sync::mpsc::{self, UnboundedReceiver},
};
use tracing::{info, warn};

use mqtt3::{proto, PROTOCOL_LEVEL, PROTOCOL_NAME};
use mqtt_broker::{
    auth::AllowAll, Auth, AuthId, BrokerBuilder, BrokerHandle, ClientEvent, ClientId, ConnReq,
    ConnectionHandle, Message, Publish, SystemEvent,
};

criterion_group!(
    basic,
    subscribe_to_separate_topic,
    subscribe_to_common_topic
);
criterion_main!(basic);

/// All subscribers subscribe to its own topic.
/// Publisher randomly selects a one of topics and publishes message to it.
/// ```
/// publisher  pub topic/{random: 1..N}
///
/// subscriber1 sub topic/1
/// subscriber2 sub topic/2
/// subscriberN sub topic/N
/// ```
fn subscribe_to_separate_topic(c: &mut Criterion) {
    init_logging();

    let (messages, subscribers) = scenarios();

    for (qos, payload_size) in messages {
        let name = format!("sub_separate_{}_q{}", payload_size, u8::from(qos));
        let mut group = c.benchmark_group(&name);

        for count in &subscribers {
            let strategy = Strategy::SeparateTopic(*count);
            dispatch_messages(&mut group, strategy, *count, payload_size, qos);
        }

        group.finish();
    }
}

/// All subscribers subscribe to the same topic.
/// Publisher always publishes messages to this topic.
/// ```
/// publisher  pub topic
///
/// subscriber1 sub topic
/// subscriber2 sub topic
/// subscriberN sub topic
/// ```
fn subscribe_to_common_topic(c: &mut Criterion) {
    init_logging();

    let (messages, subscribers) = scenarios();

    for (qos, payload_size) in messages {
        let name = format!("sub_shared_{}_q{}", payload_size, u8::from(qos));
        let mut group = c.benchmark_group(&name);

        for count in &subscribers {
            let strategy = Strategy::SharedTopic;
            dispatch_messages(&mut group, strategy, *count, payload_size, qos);
        }

        group.finish();
    }
}

fn scenarios() -> (Vec<(proto::QoS, Size)>, Vec<usize>) {
    let sizes = vec![Size::B(32), Size::Kb(1), Size::Kb(128), Size::Mb(1)];
    let qoses = vec![proto::QoS::AtMostOnce, proto::QoS::AtLeastOnce];
    let clients = vec![1, 10, 50, 100];

    (iproduct!(qoses, sizes).collect(), clients)
}

fn dispatch_messages(
    group: &mut BenchmarkGroup<WallTime>,
    strategy: Strategy,
    client_count: usize,
    size: Size,
    qos: proto::QoS,
) {
    let mut runtime = Runtime::new().expect("runtime");

    let mut broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let (on_publish_tx, mut on_publish_rx) = mpsc::unbounded_channel();
    broker.on_publish = Some(on_publish_tx);

    let broker_handle = broker.handle();

    let broker_task = runtime.spawn(broker.run());

    let subscriber_tasks: Vec<_> = (0..client_count)
        .map(|sub_id| {
            let broker_handle = broker_handle.clone();

            let id = Id::new_subscriber(sub_id);
            let topic = strategy.sub_topic(&id);

            let client = runtime.block_on(async move {
                let mut client = Client::connect(id, broker_handle.clone()).await;
                client.subscribe(topic, qos).await;

                client
            });

            runtime.spawn(client.run())
        })
        .collect();

    let id = Id::new_publisher(0);
    let publisher = runtime.block_on(Client::connect(id, broker_handle.clone()));

    let publish_handle = publisher.publish_handle();
    let publisher_task = runtime.spawn(publisher.run());

    let bench_name = format!("sub_{:03}", client_count);
    group.bench_function(bench_name, |b| {
        b.iter_custom(|iters| {
            let mut total_execution_time = Duration::from_millis(0);

            for _i in 0..iters {
                criterion::black_box(|| {
                    let topic = strategy.pub_topic(&publish_handle.id);
                    let publish = publish_handle.publish(qos, topic, size);

                    let message = Message::Client(
                        publish_handle.id.as_client_id(),
                        ClientEvent::PublishFrom(publish, None),
                    );
                    broker_handle.send(message).expect("publish");
                });

                runtime.block_on(async {
                    let execution_time = on_publish_rx.recv().await.expect("publish processed");
                    total_execution_time += execution_time;
                });
            }

            total_execution_time
        })
    });

    runtime.block_on(async move {
        broker_handle
            .send(Message::System(SystemEvent::Shutdown))
            .expect("broker shutdown event");

        futures_util::future::join_all(subscriber_tasks).await;
        publisher_task.await.expect("join publisher");
        broker_task.await.expect("join broker").expect("broker");
    });
}

fn init_logging() {
    let subscriber = tracing_subscriber::fmt::Subscriber::builder()
        .with_ansi(atty::is(atty::Stream::Stderr))
        .with_max_level(tracing::Level::INFO)
        .with_writer(std::io::stderr)
        .with_env_filter(tracing_subscriber::EnvFilter::from_default_env())
        .finish();
    let _ = tracing::subscriber::set_global_default(subscriber);
}

struct Client {
    id: Id,
    broker_handle: BrokerHandle,
    rx: UnboundedReceiver<Message>,
    pubs_to_be_acked: Arc<Mutex<HashSet<proto::PacketIdentifier>>>,
}

impl Client {
    async fn connect(client_id: Id, broker_handle: BrokerHandle) -> Self {
        let (tx, rx) = mpsc::unbounded_channel();

        let client = Self {
            id: client_id,
            broker_handle,
            rx,
            pubs_to_be_acked: Arc::new(Mutex::new(HashSet::default())),
        };

        let connection_handle = ConnectionHandle::from_sender(tx);
        let connect = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession(client.id.to_string()),
            keep_alive: Duration::from_secs(10),
            protocol_name: PROTOCOL_NAME.into(),
            protocol_level: PROTOCOL_LEVEL,
        };
        let connreq = ConnReq::new(
            client.id.as_client_id(),
            peer_addr(),
            connect,
            Auth::Identity(AuthId::Anonymous),
            connection_handle,
        );
        let message = Message::Client(client.id.as_client_id(), ClientEvent::ConnReq(connreq));
        client.broker_handle.send(message).expect("connect");

        client
    }

    async fn subscribe(&mut self, topic_filter: String, max_qos: proto::QoS) {
        let subscribe = proto::Subscribe {
            packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![proto::SubscribeTo {
                topic_filter,
                qos: max_qos,
            }],
        };
        let message = Message::Client(self.id.as_client_id(), ClientEvent::Subscribe(subscribe));
        self.broker_handle.send(message).expect("subscribe");
    }

    fn publish_handle(&self) -> PublishHandle {
        PublishHandle {
            id: self.id.clone(),
            pubs_to_be_acked: self.pubs_to_be_acked.clone(),
        }
    }

    async fn run(mut self) {
        let mut stop = false;
        let mut messages_received = 0u32;
        while let Some(message) = self.rx.recv().await {
            match message {
                Message::Client(client_id, event) => {
                    let event_out = match event {
                        ClientEvent::PublishTo(Publish::QoS0(id, publish))
                        | ClientEvent::PublishTo(Publish::QoS12(id, publish)) => {
                            messages_received += 1;

                            match publish.packet_identifier_dup_qos {
                                proto::PacketIdentifierDupQoS::AtMostOnce => {
                                    Some(ClientEvent::PubAck0(id))
                                }
                                proto::PacketIdentifierDupQoS::AtLeastOnce(id, _) => {
                                    let mut acks =
                                        self.pubs_to_be_acked.lock().expect("pubs_to_be_acked");
                                    acks.insert(id);
                                    Some(ClientEvent::PubAck(proto::PubAck {
                                        packet_identifier: id,
                                    }))
                                }
                                proto::PacketIdentifierDupQoS::ExactlyOnce(id, _) => {
                                    Some(ClientEvent::PubRec(proto::PubRec {
                                        packet_identifier: id,
                                    }))
                                }
                            }
                        }
                        ClientEvent::PubAck(puback) => {
                            let mut acks = self.pubs_to_be_acked.lock().expect("pubs_to_be_acked");
                            if !acks.remove(&puback.packet_identifier) {
                                warn!(
                                    "{}: cannot find packet identifier {}",
                                    self.id, puback.packet_identifier,
                                );
                            }
                            None
                        }
                        ClientEvent::DropConnection
                        | ClientEvent::CloseSession
                        | ClientEvent::Disconnect(_) => {
                            stop = true;
                            None
                        }
                        _ => None,
                    };

                    if let Some(event) = event_out {
                        let message = Message::Client(client_id.clone(), event);
                        if let Err(e) = self.broker_handle.send(message) {
                            warn!("{}: Broker closed connection. {:?}", client_id, e);
                            stop = true;
                        }
                    }
                }
                Message::System(_) => {}
            };

            if stop {
                info!("{}: disconnect requested", self.id);
                break;
            }
        }

        info!("{}: received {} messages", self.id, messages_received);
    }
}

struct PublishHandle {
    id: Id,
    pubs_to_be_acked: Arc<Mutex<HashSet<proto::PacketIdentifier>>>,
}

impl PublishHandle {
    fn publish(&self, qos: proto::QoS, topic_name: String, payload_size: Size) -> proto::Publish {
        let packet_id = match qos {
            proto::QoS::AtMostOnce => proto::PacketIdentifierDupQoS::AtMostOnce,
            proto::QoS::AtLeastOnce => {
                let mut acks = self.pubs_to_be_acked.lock().expect("pubs_to_be_acked");

                let mut next = proto::PacketIdentifier::new(1).unwrap();
                while !acks.insert(next) {
                    next += 1
                }

                proto::PacketIdentifierDupQoS::AtLeastOnce(next, false)
            }
            proto::QoS::ExactlyOnce => todo!(),
        };

        proto::Publish {
            packet_identifier_dup_qos: packet_id,
            retain: false,
            topic_name,
            payload: (0..payload_size.into())
                .map(|_| rand::random::<u8>())
                .collect(),
        }
    }
}

#[derive(Debug, Clone, Copy)]
enum Size {
    B(usize),
    Kb(usize),
    Mb(usize),
}

impl From<Size> for usize {
    fn from(size: Size) -> Self {
        match size {
            Size::B(value) => value,
            Size::Kb(value) => value * 1024,
            Size::Mb(value) => value * 1024 * 1024,
        }
    }
}

impl Display for Size {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            Size::B(value) => write!(f, "{}b", value),
            Size::Kb(value) => write!(f, "{}kb", value),
            Size::Mb(value) => write!(f, "{}mb", value),
        }
    }
}

#[derive(Debug, Clone)]
struct Id(usize, ClientId);

impl Id {
    fn new_subscriber(id: usize) -> Self {
        Self::new(id, format!("subscriber/{}", id))
    }

    fn new_publisher(id: usize) -> Self {
        Self::new(id, format!("publisher/{}", id))
    }

    fn new(id: usize, client_id: impl Into<ClientId>) -> Self {
        Id(id, client_id.into())
    }

    fn as_number(&self) -> usize {
        self.0
    }

    fn as_client_id(&self) -> ClientId {
        self.1.clone()
    }
}

impl Display for Id {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.1)
    }
}

const PREFIX: &str = "topic";

// NOTE Disabled due to incorrect lint warning. All variants are used in the benches
#[allow(dead_code)]
enum Strategy {
    SeparateTopic(usize),
    SharedTopic,
}

impl Strategy {
    fn sub_topic(&self, client_id: &Id) -> String {
        match self {
            Self::SeparateTopic(_) => format!("{}/{}", PREFIX, client_id.as_number()),
            Self::SharedTopic => PREFIX.into(),
        }
    }

    fn pub_topic(&self, _client_id: &Id) -> String {
        match self {
            Self::SeparateTopic(subscribers) => {
                let sub_id = rand::thread_rng().gen_range(0, subscribers);
                format!("{}/{}", PREFIX, sub_id)
            }
            Self::SharedTopic => PREFIX.into(),
        }
    }
}

fn peer_addr() -> SocketAddr {
    "127.0.0.1:12345".parse().unwrap()
}
