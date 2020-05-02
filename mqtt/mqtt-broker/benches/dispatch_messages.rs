//! `dispatch_message` benches start broker with empty state and no network connections.
//! Firstly, each bench test creates a given number of publishers, subscribers and connects
//! them to a broker. Then it makes a series of exercises to send messages to a broker and
//! measures the time between a message successfully sent to a broker and broker processed
//! this message. For this case broker extended with a `on_publish` "callback" which has to
//! be exposed only for benchmark tests.
//!
//! Scenarios supported:
//! * 1-to-1
//! * fan-in
//! * fan-out
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
    iter::FromIterator,
    sync::{Arc, Mutex},
    time::Duration,
};

use bytes::Bytes;
use criterion::{
    criterion_group, criterion_main, measurement::WallTime, BatchSize, BenchmarkGroup, Criterion,
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
    AuthId, BrokerBuilder, BrokerHandle, ClientEvent, ClientId, ConnReq, ConnectionHandle, Message,
    Publish, SystemEvent,
};

criterion_group!(basic, one_to_one, fan_in, fan_out);
criterion_main!(basic);

/// Each publisher sends messages to one corresponding subscriber
/// ```
/// pub1 pub topic/1            sub1 sub topic/1
/// pub2 pub topic/2            sub2 sub topic/2
/// pub2 pub topic/N            sub2 sub topic/N
/// ```
fn one_to_one(c: &mut Criterion) {
    init_logging();

    let mut group = c.benchmark_group("1-to-1");
    for (qos, count, payload_size) in scenarios() {
        let strategy = Strategy::new_one_to_one(count, count);
        dispatch_messages(&mut group, strategy, count, payload_size, qos);
    }
    group.finish();
}

/// All publishers send message to a randomly chose topic
/// ```
/// pub1 pub topic/2            sub1 sub topic/1
/// pub2 pub topic/2            sub2 sub topic/2
/// pubN pub topic/2            sub2 sub topic/N
/// ```
fn fan_in(c: &mut Criterion) {
    init_logging();

    let mut group = c.benchmark_group("fan_in");
    for (qos, count, payload_size) in scenarios() {
        let strategy = Strategy::new_fan_in(count, count);
        dispatch_messages(&mut group, strategy, count, payload_size, qos);
    }
    group.finish();
}

/// Multiple publishers sends messages to a topic all subscribers subscribed to
/// ```
/// pub1 pub topic/foo            sub1 sub topic/foo
/// pub2 pub topic/foo            sub2 sub topic/foo
/// pubN pub topic/foo            sub2 sub topic/foo
/// ```
fn fan_out(c: &mut Criterion) {
    init_logging();

    let mut group = c.benchmark_group("fan_out");
    for (qos, count, payload_size) in scenarios() {
        let strategy = Strategy::new_fan_out(count, count);
        dispatch_messages(&mut group, strategy, count, payload_size, qos);
    }
    group.finish();
}

fn scenarios() -> impl IntoIterator<Item = (proto::QoS, usize, Size)> {
    let sizes = vec![Size::B(32), Size::Kb(128), Size::Mb(1)];
    let clients = vec![1, 10, 100];
    let qoses = vec![proto::QoS::AtMostOnce, proto::QoS::AtLeastOnce];

    iproduct!(qoses, clients, sizes)
}

fn dispatch_messages(
    group: &mut BenchmarkGroup<WallTime>,
    strategy: Strategy,
    client_count: usize,
    size: Size,
    qos: proto::QoS,
) {
    let mut runtime = Runtime::new().expect("runtime");

    let mut broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (on_publish_tx, on_publish_rx) = crossbeam_channel::bounded(1024);
    broker.on_publish = Some(on_publish_tx);

    let mut broker_handle = broker.handle();

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

    let (publisher_handles, publisher_tasks): (Vec<_>, Vec<_>) = (0..client_count)
        .map(|pub_id| {
            let id = Id::new_publisher(pub_id);
            let client = runtime.block_on(Client::connect(id, broker_handle.clone()));

            let publish_handle = client.publish_handle();
            let task = runtime.spawn(client.run());
            (publish_handle, task)
        })
        .unzip();

    let bench_name = format!("sub_{}/msg_{}_q{}", client_count, size, u8::from(qos));
    group.bench_function(bench_name, |b| {
        b.iter_batched(
            || {
                let pub_index = rand::thread_rng().gen_range(0, client_count);
                let publisher = publisher_handles.get(pub_index).expect("publisher");
                let topic = strategy.pub_topic(&publisher.id);
                let publish = publisher.publish(qos, topic, size);

                Message::Client(
                    publisher.id.as_client_id(),
                    ClientEvent::PublishFrom(publish),
                )
            },
            |message| {
                runtime.block_on(async {
                    broker_handle.send(message).expect("publish");
                    on_publish_rx.recv().expect("publish processed");
                });
            },
            BatchSize::SmallInput,
        )
    });

    let tasks: Vec<_> = subscriber_tasks
        .into_iter()
        .chain(publisher_tasks.into_iter())
        .collect();

    runtime.block_on(async move {
        broker_handle
            .send(Message::System(SystemEvent::Shutdown))
            .expect("broker shutdown event");

        futures_util::future::join_all(tasks).await;
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

        let mut client = Self {
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
        let connreq = ConnReq::new(client.id.as_client_id(), connect, None, connection_handle);
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
            payload: Bytes::from_iter((0..payload_size.into()).map(|_| rand::random::<u8>())),
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

impl std::fmt::Display for Size {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
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

impl std::fmt::Display for Id {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.1)
    }
}

const PREFIX: &str = "topic";

// NOTE Disabled due to incorrect lint warning. All variants are used in the benches
#[allow(dead_code)]
enum Strategy {
    OneToOne {
        subs: usize,
        pubs: usize,
    },
    FanIn {
        subs: usize,
        pubs: usize,
        topic: String,
    },
    FanOut {
        subs: usize,
        pubs: usize,
    },
}

impl Strategy {
    fn new_one_to_one(pubs: usize, subs: usize) -> Self {
        Self::OneToOne { subs, pubs }
    }

    fn new_fan_in(pubs: usize, subs: usize) -> Self {
        let sub_id = rand::thread_rng().gen_range(0, subs);
        let topic = format!("{}/{}", PREFIX, sub_id);
        Self::FanIn { subs, pubs, topic }
    }

    fn new_fan_out(pubs: usize, subs: usize) -> Self {
        Self::FanOut { subs, pubs }
    }

    fn sub_topic(&self, client_id: &Id) -> String {
        match self {
            Self::OneToOne { .. } => format!("{}/{}", PREFIX, client_id.as_number()),
            Self::FanIn { .. } => format!("{}/{}", PREFIX, client_id.as_number()),
            Self::FanOut { .. } => PREFIX.into(),
        }
    }

    fn pub_topic(&self, client_id: &Id) -> String {
        match self {
            Self::OneToOne { .. } => format!("{}/{}", PREFIX, client_id.as_number()),
            Self::FanIn { topic, .. } => topic.into(),
            Self::FanOut { .. } => PREFIX.into(),
        }
    }
}
