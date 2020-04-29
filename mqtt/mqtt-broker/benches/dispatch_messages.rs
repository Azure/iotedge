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
use tokio::{runtime::Runtime, sync::mpsc};
use tracing::{info, warn};

use mpsc::UnboundedReceiver;
use mqtt3::{proto, PROTOCOL_LEVEL, PROTOCOL_NAME};
use mqtt_broker::{
    AuthId, BrokerBuilder, BrokerHandle, ClientEvent, ClientId, ConnReq, ConnectionHandle, Message,
    Publish, SystemEvent,
};

criterion_group!(basic, one_to_one);
criterion_main!(basic);

fn one_to_one(c: &mut Criterion) {
    init_logging();

    let sizes = vec![Size::B(32), Size::Kb(128), Size::Mb(1)];
    let subscribers = vec![1, 10, 100];
    let qoses = vec![proto::QoS::AtMostOnce, proto::QoS::AtLeastOnce];

    let mut group = c.benchmark_group("1-to-1");
    for (qos, sub_count, payload_size) in iproduct!(qoses, subscribers, sizes) {
        dispatch_messages(&mut group, sub_count, payload_size, qos);
    }
    group.finish();
}

fn dispatch_messages(
    group: &mut BenchmarkGroup<WallTime>,
    sub_count: usize,
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

    let subscriber_tasks: Vec<_> = (0..sub_count)
        .map(|sub_id| {
            let broker_handle = broker_handle.clone();

            let client = runtime.block_on(async move {
                let client_id = format!("subscriber/{}", sub_id);
                let mut client = Client::connect(client_id.clone(), broker_handle.clone()).await;
                let topic = client_id.replace("subscriber", "topic");
                client.subscribe(topic, qos).await;

                client
            });

            runtime.spawn(client.run())
        })
        .collect();

    let (publisher_handles, publisher_tasks): (Vec<_>, Vec<_>) = (0..1)
        .map(|pub_id| {
            let client_id = format!("publisher/{}", pub_id);
            let client = runtime.block_on(Client::connect(client_id, broker_handle.clone()));

            let publish_handle = client.publish_handle();
            let task = runtime.spawn(client.run());
            (publish_handle, task)
        })
        .unzip();

    let bench_name = format!("q{}/sub/{}/msg/{}", u8::from(qos), sub_count, size);
    group.bench_function(bench_name, |b| {
        b.iter_batched(
            || {
                let publisher = publisher_handles.get(0).expect("publisher");
                let publish = publisher.publish(qos, "topic/0".into(), size);

                Message::Client(
                    publisher.client_id.clone(),
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
        .finish();
    let _ = tracing::subscriber::set_global_default(subscriber);
}

struct Client {
    client_id: ClientId,
    broker_handle: BrokerHandle,
    rx: UnboundedReceiver<Message>,
    pubs_to_be_acked: Arc<Mutex<HashSet<proto::PacketIdentifier>>>,
}

impl Client {
    async fn connect(client_id: String, broker_handle: BrokerHandle) -> Self {
        let (tx, rx) = mpsc::unbounded_channel();

        let mut client = Self {
            client_id: client_id.into(),
            broker_handle,
            rx,
            pubs_to_be_acked: Arc::new(Mutex::new(HashSet::default())),
        };

        let connection_handle = ConnectionHandle::from_sender(tx);
        let connect = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession(client.client_id.to_string()),
            keep_alive: Duration::from_secs(10),
            protocol_name: PROTOCOL_NAME.into(),
            protocol_level: PROTOCOL_LEVEL,
        };
        let connreq = ConnReq::new(client.client_id.clone(), connect, None, connection_handle);
        let message = Message::Client(client.client_id.clone(), ClientEvent::ConnReq(connreq));
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
        let message = Message::Client(self.client_id.clone(), ClientEvent::Subscribe(subscribe));
        self.broker_handle.send(message).expect("subscribe");
    }

    fn publish_handle(&self) -> PublishHandle {
        PublishHandle {
            client_id: self.client_id.clone(),
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
                        ClientEvent::PublishFrom(_) => None,
                        ClientEvent::PubAck0(_) => None,
                        ClientEvent::PubAck(puback) => {
                            let mut acks = self.pubs_to_be_acked.lock().expect("pubs_to_be_acked");
                            if !acks.remove(&puback.packet_identifier) {
                                warn!(
                                    "{}: cannot find packet identifier {}",
                                    self.client_id, puback.packet_identifier,
                                );
                            }
                            None
                        }
                        ClientEvent::PubRec(_) => None,
                        ClientEvent::PubRel(_) => None,
                        ClientEvent::PubComp(_) => None,
                        ClientEvent::DropConnection
                        | ClientEvent::CloseSession
                        | ClientEvent::Disconnect(_) => {
                            stop = true;
                            None
                        }
                        ClientEvent::PingReq(_) => None,
                        ClientEvent::PingResp(_) => None,
                        ClientEvent::ConnReq(_) => None,
                        ClientEvent::ConnAck(_) => None,
                        ClientEvent::Subscribe(_) => None,
                        ClientEvent::SubAck(_) => None,
                        ClientEvent::Unsubscribe(_) => None,
                        ClientEvent::UnsubAck(_) => None,
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
                info!("{}: disconnect requested", self.client_id);
                break;
            }
        }

        info!(
            "{}: received {} messages",
            self.client_id, messages_received
        );
    }
}

struct PublishHandle {
    client_id: ClientId,
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
