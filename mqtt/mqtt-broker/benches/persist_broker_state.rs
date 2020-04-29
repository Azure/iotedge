use std::collections::HashMap;
use std::iter::FromIterator;

use bytes::Bytes;
use criterion::{
    criterion_group, criterion_main, measurement::WallTime, BatchSize, BenchmarkGroup, Criterion,
};
use tempfile::TempDir;

use mqtt3::proto::{Publication, QoS};
use mqtt_broker::{
    BrokerState, ClientId, ConsolidatedStateFormat, FileFormat, FilePersistor, Persist,
    PersistError, SessionState,
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

                (state, persistor, tmp_dir)
            },
            |(state, mut persistor, _tmp_dir)| persistor.store(state).expect("store"),
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
                persistor.store(state).expect("store");

                (persistor, tmp_dir)
            },
            |(mut persistor, _tmp_dir)| persistor.load().expect("load"),
            BatchSize::SmallInput,
        );
    });
}

fn make_fake_state(
    num_clients: u32,
    num_unique_messages: u32,
    num_shared_messages: u32,
    num_retained: u32,
) -> BrokerState {
    let retained = HashMap::from_iter((0..num_retained).map(|i| {
        (
            format!("Retained {}", i),
            make_fake_publish(format!("Retained {}", i)),
        )
    }));

    let shared_messages: Vec<Publication> = (0..num_shared_messages)
        .map(|_| make_fake_publish("Shared Topic".to_owned()))
        .collect();

    let sessions = (0..num_clients)
        .map(|i| {
            let waiting_to_be_sent = (0..num_unique_messages)
                .map(|_| make_fake_publish(format!("Topic {}", i)))
                .chain(shared_messages.clone())
                .collect();

            SessionState::from_parts(
                ClientId::from(format!("Session {}", i)),
                HashMap::new(),
                waiting_to_be_sent,
            )
        })
        .collect();

    BrokerState::new(retained, sessions)
}

fn make_fake_publish(topic_name: String) -> Publication {
    Publication {
        topic_name,
        retain: false,
        qos: QoS::AtLeastOnce,
        payload: make_random_payload(10),
    }
}

fn make_random_payload(size: u32) -> Bytes {
    Bytes::from_iter((0..size).map(|_| rand::random::<u8>()))
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
            ConsolidatedStateFormat::default(),
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
            ConsolidatedStateFormat::default(),
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
