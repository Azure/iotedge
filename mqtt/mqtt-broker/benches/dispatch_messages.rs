use bytes::Bytes;
use criterion::{
    criterion_group, criterion_main, measurement::WallTime, BatchSize, BenchmarkGroup, Criterion,
};
use itertools::iproduct;
use mqtt3::{proto, PROTOCOL_LEVEL, PROTOCOL_NAME};
use mqtt_broker::{
    AuthId, BrokerBuilder, BrokerHandle, ClientEvent, ClientId, ConnReq, ConnectionHandle, Message,
    Publish, SystemEvent,
};
use std::{
    collections::{HashMap, HashSet},
    iter::FromIterator,
    sync::{Arc, Mutex, RwLock},
    time::Duration,
};
use tokio::{
    runtime::Runtime,
    sync::mpsc::{self, Receiver, Sender},
    task::JoinHandle,
};
use tracing::{info, warn};

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

fn dispatch_messages(c: &mut Criterion) {
    let sizes = vec![Size::B(32), Size::Kb(128), Size::Mb(1)];
    let subscribers = vec![1, 10, 100];
    let qoses = vec![proto::QoS::AtMostOnce, proto::QoS::AtLeastOnce];

    let mut group = c.benchmark_group("messages_q0");
    for (sub_count, payload_size) in iproduct!(subscribers.iter(), sizes.iter()) {
        dispatch_fan_out(
            &mut group,
            *sub_count,
            *payload_size,
            proto::QoS::AtMostOnce,
        );
    }

    // let mut group = c.benchmark_group("messages_q1");
    // for (sub_count, payload_size) in iproduct!(subscribers, sizes) {
    //     dispatch_fan_out(&mut group, sub_count, payload_size, proto::QoS::AtMostOnce);
    // }

    group.finish();
}

fn dispatch_fan_out(
    group: &mut BenchmarkGroup<WallTime>,
    sub_count: usize,
    size: Size,
    qos: proto::QoS,
) {
    let mut runtime = Runtime::new().expect("runtime");

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();
    let mut broker_handle = broker.handle();

    let broker_task = runtime.spawn(broker.run());

    let client_tasks: Vec<_> = (0..sub_count)
        .map(|sub_id| {
            let client_id = format!("subscriber/{}", sub_id);

            let broker_handle = broker_handle.clone();
            let client = runtime.block_on(async move {
                let mut client = Client::connect(client_id.clone(), broker_handle.clone()).await;
                let topic = client_id.replace("subscriber", "topic");
                client.subscribe(topic, proto::QoS::AtLeastOnce).await;

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

    // let (tx, mut rx) = mpsc::channel(1024);
    // let mut broker_handle1 = broker_handle.clone();
    // runtime.spawn(async move {
    //     let mut stop = false;
    //     let mut messages_received = 0u32;
    //     while let Some(message) = rx.recv().await {
    //         match message {
    //             Message::Client(client_id, event) => {
    //                 let event_out = match event {
    //                     ClientEvent::PublishTo(Publish::QoS0(id, publish))
    //                     | ClientEvent::PublishTo(Publish::QoS12(id, publish)) => {
    //                         messages_received += 1;

    //                         match publish.packet_identifier_dup_qos {
    //                             proto::PacketIdentifierDupQoS::AtMostOnce => {
    //                                 Some(ClientEvent::PubAck0(id))
    //                             }
    //                             proto::PacketIdentifierDupQoS::AtLeastOnce(id, _) => {
    //                                 Some(ClientEvent::PubAck(proto::PubAck {
    //                                     packet_identifier: id,
    //                                 }))
    //                             }
    //                             proto::PacketIdentifierDupQoS::ExactlyOnce(id, _) => {
    //                                 Some(ClientEvent::PubRec(proto::PubRec {
    //                                     packet_identifier: id,
    //                                 }))
    //                             }
    //                         }
    //                     }
    //                     ClientEvent::PublishFrom(_) => None,
    //                     ClientEvent::PubAck0(_) => None,
    //                     ClientEvent::PubAck(_) => {
    //                         dbg!("ack");
    //                         None
    //                     }
    //                     ClientEvent::PubRec(_) => None,
    //                     ClientEvent::PubRel(_) => None,
    //                     ClientEvent::PubComp(_) => None,
    //                     ClientEvent::DropConnection
    //                     | ClientEvent::CloseSession
    //                     | ClientEvent::Disconnect(_) => {
    //                         stop = true;
    //                         None
    //                     }
    //                     ClientEvent::PingReq(_) => Some(ClientEvent::PingResp(proto::PingResp)),
    //                     ClientEvent::PingResp(_) => Some(ClientEvent::PingReq(proto::PingReq)),
    //                     ClientEvent::ConnReq(_) => None,
    //                     ClientEvent::ConnAck(_) => None,
    //                     ClientEvent::Subscribe(_) => None,
    //                     ClientEvent::SubAck(_) => None,
    //                     ClientEvent::Unsubscribe(_) => None,
    //                     ClientEvent::UnsubAck(_) => None,
    //                 };

    //                 if let Some(event) = event_out {
    //                     let message = Message::Client(client_id.clone(), event);
    //                     if let Err(e) = broker_handle1.send(message).await {
    //                         println!("WARN: Broker closed connection for {}. {:?}", client_id, e);
    //                         break;
    //                     }
    //                 }
    //             }
    //             Message::System(_) => {}
    //         };

    //         if stop {
    //             println!("INFO: disconnect requested");
    //             break;
    //         }
    //     }

    //     let client_id = "publisher/0";
    //     println!(
    //         "INFO: Client: {} received {} messages",
    //         client_id, messages_received
    //     );
    // });
    // runtime.block_on(start_publisher(0, broker_handle.clone(), tx));

    let bench_name = format!("sub/{}/msg/{}/qos/{}", sub_count, size, u8::from(qos));
    group.sample_size(100);
    group.bench_function(bench_name, |b| {
        b.iter_batched(
            || {
                // let qos = match payload_qos {
                //     0 => proto::PacketIdentifierDupQoS::AtMostOnce,
                //     1 => proto::PacketIdentifierDupQoS::AtLeastOnce(
                //         proto::PacketIdentifier::new(),
                //         false,
                //     ),
                //     2 => proto::PacketIdentifierDupQoS::AtLeastOnce(
                //         proto::PacketIdentifier::new(),
                //         false,
                //     ),
                // };

                let publisher = publisher_handles.get(0).expect("publisher");
                let publish = publisher.publish(qos, "topic/0".into(), size);

                Message::Client(
                    publisher.client_id.clone(),
                    ClientEvent::PublishFrom(publish),
                )

                // let client_id = "publisher/0".into();
                // let topic_name = "topic/0".into();
                // let publish = proto::Publish {
                //     packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
                //     retain: false,
                //     topic_name,
                //     payload: Bytes::from_iter(
                //         (0..payload_size.into()).map(|_| rand::random::<u8>()),
                //     ),
                // };
                // Message::Client(client_id, ClientEvent::PublishFrom(publish))
            },
            |message| {
                runtime.block_on(async {
                    broker_handle.send(message).await.unwrap();
                    // dbg!("send");
                })
            },
            BatchSize::SmallInput,
        )
    });

    let tasks: Vec<_> = client_tasks
        .into_iter()
        .chain(publisher_tasks.into_iter())
        .collect();

    runtime.block_on(async move {
        broker_handle
            .send(Message::System(SystemEvent::Shutdown))
            .await
            .expect("broker shutdown event");

        futures_util::future::join_all(tasks).await;
        broker_task.await.expect("broker task");
    });
}

async fn start_subscriber(client_id: String, mut broker_handle: BrokerHandle, tx: Sender<Message>) {
    let subscriber_id = ClientId::from(client_id.clone());

    let connection_handle = ConnectionHandle::from_sender(tx);
    let connect = proto::Connect {
        username: None,
        password: None,
        will: None,
        client_id: proto::ClientId::IdWithCleanSession(client_id.clone()),
        keep_alive: Duration::from_secs(10),
        protocol_name: PROTOCOL_NAME.into(),
        protocol_level: PROTOCOL_LEVEL,
    };
    let connreq = ConnReq::new(subscriber_id.clone(), connect, None, connection_handle);
    let message = Message::Client(subscriber_id.clone(), ClientEvent::ConnReq(connreq));
    broker_handle.send(message).await.unwrap();

    let subscribe = proto::Subscribe {
        packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
        subscribe_to: vec![proto::SubscribeTo {
            topic_filter: client_id.replace("subscriber", "topic"),
            qos: proto::QoS::AtMostOnce,
        }],
    };
    let message = Message::Client(subscriber_id, ClientEvent::Subscribe(subscribe));
    broker_handle.send(message).await.unwrap();
}

async fn start_publisher(pub_id: usize, mut broker_handle: BrokerHandle, tx: Sender<Message>) {
    let client_id = format!("publisher/{}", pub_id);
    let publisher_id = ClientId::from(client_id.clone());

    let connection_handle = ConnectionHandle::from_sender(tx);
    let connect = proto::Connect {
        username: None,
        password: None,
        will: None,
        client_id: proto::ClientId::IdWithCleanSession(client_id),
        keep_alive: std::time::Duration::from_secs(2),
        protocol_name: PROTOCOL_NAME.into(),
        protocol_level: PROTOCOL_LEVEL,
    };
    let connreq = ConnReq::new(publisher_id.clone(), connect, None, connection_handle);
    let message = Message::Client(publisher_id.clone(), ClientEvent::ConnReq(connreq));
    broker_handle.send(message).await.unwrap();

    // let publish = proto::Publish {
    //     packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
    //     retain: false,
    //     topic_name: format!("test/{}", 0),
    //     payload: Bytes::from_iter((0..100.into()).map(|_| rand::random::<u8>())),
    // };
    // let message = Message::Client(publisher_id, ClientEvent::PublishFrom(publish));
    // broker_handle.send(message).await.unwrap();
}

criterion_group!(basic, dispatch_messages);
criterion_main!(basic);

struct Client {
    client_id: ClientId,
    broker_handle: BrokerHandle,
    rx: Receiver<Message>,

    pubs_to_be_acked: Arc<Mutex<HashSet<proto::PacketIdentifier>>>,
}

impl Client {
    // fn new(client_id: String, mut broker_handle: BrokerHandle, tx: Sender<Message>) -> (Self) {
    //     let client = Self {
    //         client_id: client_id.into(),
    //         broker_handle,
    //     };
    //     (client)
    // }

    async fn connect(client_id: String, broker_handle: BrokerHandle) -> Self {
        let (tx, rx) = mpsc::channel(1024);

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
        client.broker_handle.send(message).await.expect("connect");

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
        self.broker_handle.send(message).await.unwrap();
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
                        ClientEvent::PingReq(_) => Some(ClientEvent::PingResp(proto::PingResp)),
                        ClientEvent::PingResp(_) => Some(ClientEvent::PingReq(proto::PingReq)),
                        ClientEvent::ConnReq(_) => None,
                        ClientEvent::ConnAck(_) => None,
                        ClientEvent::Subscribe(_) => None,
                        ClientEvent::SubAck(_) => None,
                        ClientEvent::Unsubscribe(_) => None,
                        ClientEvent::UnsubAck(_) => None,
                    };

                    if let Some(event) = event_out {
                        let message = Message::Client(client_id.clone(), event);
                        if let Err(e) = self.broker_handle.send(message).await {
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
