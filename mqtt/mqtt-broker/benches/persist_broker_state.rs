use std::{collections::HashMap, net::SocketAddr};

use bytes::Bytes;
use chrono::Utc;
use criterion::{
    criterion_group, criterion_main, measurement::WallTime, BatchSize, BenchmarkGroup, Criterion,
};
use tempfile::TempDir;
use tokio::runtime::Runtime;

use mqtt3::proto::{PacketIdentifier, PacketIdentifierDupQoS, Publication, Publish, QoS};
use mqtt_broker::{
    AuthId, BrokerSnapshot, ClientId, ClientInfo, FileFormat, FilePersistor, Persist, PersistError,
    SessionSnapshot, VersionedFileFormat,
};

fn test_write<F>(
    group: &mut BenchmarkGroup<WallTime>,
    num_clients: u32,
    num_unique_messages: u32,
    num_shared_messages: u32,
    num_retained: u32,
    format: F,
) where
    F: FileFormat<Error = PersistError> + Clone + Send + 'static,
{
    let name = format!(
        "w/uniq_p/{}/shr_p/{}/ses/{}/ret/{}",
        num_unique_messages, num_shared_messages, num_clients, num_retained
    );

    group.bench_function(&name, |b| {
        b.iter_batched(
            || {
                let state = make_fake_state(
                    num_clients,
                    num_unique_messages,
                    num_shared_messages,
                    num_retained,
                );

                let tmp_dir = TempDir::new().unwrap();
                let path = tmp_dir.path().to_owned();
                let persistor = FilePersistor::new(path, format.clone());
                let runtime = Runtime::new().unwrap();

                (state, persistor, runtime, tmp_dir)
            },
            |(state, mut persistor, mut runtime, _tmp_dir)| {
                runtime.block_on(persistor.store(state)).expect("store")
            },
            BatchSize::SmallInput,
        );
    });
}

fn test_read<F>(
    group: &mut BenchmarkGroup<WallTime>,
    num_clients: u32,
    num_unique_messages: u32,
    num_shared_messages: u32,
    num_retained: u32,
    format: F,
) where
    F: FileFormat<Error = PersistError> + Clone + Send + 'static,
{
    let name = format!(
        "r/uniq_p/{}/shr_p/{}/ses/{}/ret/{}",
        num_unique_messages, num_shared_messages, num_clients, num_retained
    );

    group.bench_function(&name, |b| {
        b.iter_batched(
            || {
                let state = make_fake_state(
                    num_clients,
                    num_unique_messages,
                    num_shared_messages,
                    num_retained,
                );

                let tmp_dir = TempDir::new().unwrap();
                let path = tmp_dir.path().to_owned();
                let mut persistor = FilePersistor::new(path, format.clone());

                let mut runtime = Runtime::new().unwrap();
                runtime.block_on(persistor.store(state)).expect("store");

                (persistor, runtime, tmp_dir)
            },
            |(mut persistor, mut runtime, _tmp_dir)| {
                runtime.block_on(persistor.load()).expect("load")
            },
            BatchSize::SmallInput,
        );
    });
}

fn make_fake_state(
    num_clients: u32,
    num_unique_messages: u32,
    num_shared_messages: u32,
    num_retained: u32,
) -> BrokerSnapshot {
    let retained = (0..num_retained)
        .map(|i| {
            (
                format!("Retained {}", i),
                make_fake_publication(format!("Retained {}", i)),
            )
        })
        .collect();

    let shared_messages: Vec<Publication> = (0..num_shared_messages)
        .map(|_| make_fake_publication("Shared Topic".to_owned()))
        .collect();

    let sessions = (0..num_clients)
        .map(|i| {
            let waiting_to_be_sent = (0..num_unique_messages)
                .map(|_| make_fake_publication(format!("Topic {}", i)))
                .chain(shared_messages.clone())
                .collect();

            let waiting_to_be_acked = (0..num_unique_messages)
                .map(|i| make_fake_publish(i + 1, format!("Topic {}", i)))
                .collect();

            let id = format!("Session {}", i);
            let client_id = ClientId::from(&id);
            let auth_id = AuthId::from_identity(id);

            SessionSnapshot::from_parts(
                ClientInfo::new(client_id, peer_addr(), auth_id),
                HashMap::new(),
                waiting_to_be_sent,
                waiting_to_be_acked,
                Utc::now(),
            )
        })
        .collect();

    BrokerSnapshot::new(retained, sessions)
}

fn peer_addr() -> SocketAddr {
    "127.0.0.1:12345".parse().unwrap()
}

fn make_fake_publication(topic_name: String) -> Publication {
    Publication {
        topic_name,
        retain: false,
        qos: QoS::AtLeastOnce,
        payload: make_random_payload(10),
    }
}

fn make_fake_publish(id: u32, topic_name: String) -> Publish {
    Publish {
        topic_name,
        retain: false,
        payload: make_random_payload(10),
        packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(
            PacketIdentifier::new(id as u16).unwrap(),
            false,
        ),
    }
}

fn make_random_payload(size: u32) -> Bytes {
    (0..size).map(|_| rand::random::<u8>()).collect()
}

fn write_state(c: &mut Criterion) {
    let mut group = c.benchmark_group("write_state");
    for (clients, unique, shared, retained) in scenarios() {
        test_write(
            &mut group,
            clients,
            unique,
            shared,
            retained,
            VersionedFileFormat::default(),
        );
    }
    group.finish();
}

fn read_state(c: &mut Criterion) {
    let mut group = c.benchmark_group("read_state");
    for (clients, unique, shared, retained) in scenarios() {
        test_read(
            &mut group,
            clients,
            unique,
            shared,
            retained,
            VersionedFileFormat::default(),
        );
    }
    group.finish();
}

fn scenarios() -> Vec<(u32, u32, u32, u32)> {
    vec![
        (1, 1, 0, 0),
        (10, 10, 10, 10),
        (10, 100, 0, 0),
        (10, 0, 100, 0),
    ]
}

criterion_group!(basic, write_state, read_state);
criterion_main!(basic);
