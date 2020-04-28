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
use std::{iter::FromIterator, time::Duration};
use tokio::{
    runtime::Runtime,
    sync::mpsc::{self, Sender},
};

fn dispatch_messages_(c: &mut Criterion) {
    let mut runtime = Runtime::new().expect("runtime");

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();
    let mut broker_handle = broker.handle();

    runtime.spawn(broker.run());

    let (tx, mut rx) = mpsc::channel(1024);

    runtime.spawn(async move {
        use futures_util::StreamExt;
        while let Some(message) = rx.next().await {
            // dbg!(message);
        }
        dbg!("finished");
    });

    let mut broker_handle_sub = broker_handle.clone();
    runtime.block_on(async move {
        let subscriber_id = ClientId::from("subscriber_1");

        let connection_handle = ConnectionHandle::from_sender(tx);
        let connect = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("subscriber_1".into()),
            keep_alive: std::time::Duration::from_secs(2),
            protocol_name: PROTOCOL_NAME.into(),
            protocol_level: PROTOCOL_LEVEL,
        };
        let connreq = ConnReq::new(subscriber_id.clone(), connect, None, connection_handle);
        let message = Message::Client(subscriber_id.clone(), ClientEvent::ConnReq(connreq));
        broker_handle_sub.send(message).await.unwrap();

        let subscribe = proto::Subscribe {
            packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![proto::SubscribeTo {
                topic_filter: "test".into(),
                qos: proto::QoS::AtLeastOnce,
            }],
        };
        let message = Message::Client(subscriber_id, ClientEvent::Subscribe(subscribe));
        broker_handle_sub.send(message).await.unwrap();
    });

    c.bench_function("dispatch_messages", |b| {
        b.iter_batched(
            || {
                let publish = proto::Publish {
                    packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
                    retain: false,
                    topic_name: "test".into(),
                    payload: Bytes::from_iter((0..1024).map(|_| rand::random::<u8>())),
                };
                let client_id = "subscriber_1".into();
                let message = Message::Client(client_id, ClientEvent::PublishFrom(publish));

                message
            },
            |message| {
                runtime.block_on(async {
                    // for _ in 0..100 {
                    broker_handle.send(message).await;
                    // }
                })
            },
            BatchSize::SmallInput,
        )
    });

    runtime
        .block_on(broker_handle.send(Message::System(SystemEvent::Shutdown)))
        .unwrap();
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

fn dispatch_messages(c: &mut Criterion) {
    let mut group = c.benchmark_group("dispatch_messages");
    let sizes = vec![Size::B(32), Size::Kb(128), Size::Mb(1)];
    let subscribers = vec![1, 10, 100, 1000];
    let qoses = vec![0, 1];

    for (sub_count, payload_size, payload_qos) in iproduct!(subscribers, sizes, qoses).take(2) {
        dispatch_fan_out(&mut group, sub_count, payload_size, payload_qos);
    }

    group.finish();
}

fn dispatch_fan_out(
    group: &mut BenchmarkGroup<WallTime>,
    sub_count: usize,
    payload_size: Size,
    payload_qos: i32,
) {
    let mut runtime = Runtime::new().expect("runtime");

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();
    let mut broker_handle = broker.handle();

    let broker_task = runtime.spawn(broker.run());

    let client_handles: Vec<_> = (0..sub_count)
        .map(|sub_id| {
            let client_id = format!("subscriber/{}", sub_id);
            let (tx, mut rx) = mpsc::channel(1024);

            let mut messages_received = 0;

            let mut broker_handle_sub = broker_handle.clone();
            let subscriber_id = client_id.clone();
            let handle = runtime.spawn(async move {
                let mut stop = false;
                while let Some(message) = rx.recv().await {
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
                                ClientEvent::PubAck(_) => {
                                    dbg!("ack");
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
                                ClientEvent::PingReq(_) => {
                                    Some(ClientEvent::PingResp(proto::PingResp))
                                }
                                ClientEvent::PingResp(_) => {
                                    Some(ClientEvent::PingReq(proto::PingReq))
                                }
                                ClientEvent::ConnReq(_) => None,
                                ClientEvent::ConnAck(_) => None,
                                ClientEvent::Subscribe(_) => None,
                                ClientEvent::SubAck(_) => None,
                                ClientEvent::Unsubscribe(_) => None,
                                ClientEvent::UnsubAck(_) => None,
                            };

                            if let Some(event) = event_out {
                                let message = Message::Client(client_id.clone(), event);
                                if let Err(e) = broker_handle_sub.send(message).await {
                                    println!(
                                        "WARN: Broker closed connection for {}. {:?}",
                                        client_id, e
                                    );
                                    break;
                                }
                            }
                        }
                        Message::System(_) => {}
                    };

                    if stop {
                        println!("INFO: disconnect requested");
                        break;
                    }
                }

                println!(
                    "INFO: Client: {} received {} messages",
                    client_id, messages_received
                );
            });

            runtime.block_on(start_subscriber(subscriber_id, broker_handle.clone(), tx));
            handle
        })
        .collect();

    let (tx, _rx) = mpsc::channel(1024);
    runtime.block_on(start_publisher(0, broker_handle.clone(), tx));

    let bench_name = format!("sub/{}/msg/{}/qos/{}", sub_count, payload_size, payload_qos);
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

                let qos = proto::PacketIdentifierDupQoS::AtMostOnce;

                let publish = proto::Publish {
                    packet_identifier_dup_qos: qos,
                    retain: false,
                    topic_name: "topic/0".into(),
                    payload: Bytes::from_iter(
                        (0..payload_size.into()).map(|_| rand::random::<u8>()),
                    ),
                };
                let client_id = "publisher/0".into();
                Message::Client(client_id, ClientEvent::PublishFrom(publish))
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

    runtime.block_on(async {
        broker_handle
            .send(Message::System(SystemEvent::Shutdown))
            .await
            .expect("broker shutdown event");
        futures_util::future::join_all(client_handles).await;
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
