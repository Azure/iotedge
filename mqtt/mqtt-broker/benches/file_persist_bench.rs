use std::collections::{HashMap, VecDeque};
use std::iter::FromIterator;

use bytes::Bytes;
use criterion::*;
use mqtt3::proto::{Publication, QoS};
use mqtt_broker::{
    BincodeFormat, BrokerState, ClientId, ConsolidatedStateFormat, FilePersistor, Persist,
    SessionState,
};
use rand::Rng;
use tempfile::TempDir;
use tokio::runtime::Runtime;

fn test_write(
    c: &mut Criterion,
    num_clients: u32,
    num_unique_messages: u32,
    num_shared_messages: u32,
    num_retained: u32,
) {
    let name = format!(
        "Write {} unique and {} shared messages for {} sessions with {} retained messages",
        num_unique_messages, num_shared_messages, num_clients, num_retained
    );

    c.bench_function(&name, |b| {
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
                let persistor = FilePersistor::new(path, ConsolidatedStateFormat::new());
                let rt = Runtime::new().unwrap();

                (state, persistor, rt, tmp_dir)
            },
            |(state, mut persistor, mut rt, _tmp_dir)| {
                rt.block_on(async { persistor.store(state).await })
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
            let mut session = SessionState::new(ClientId::from(format!("Session {}", i)));
            let waiting_to_be_sent = (0..num_unique_messages)
                .map(|_| make_fake_publish(format!("Topic {}", i)))
                .chain(shared_messages.clone());
            session.waiting_to_be_sent = VecDeque::from_iter(waiting_to_be_sent);

            session
        })
        .collect();

    BrokerState { retained, sessions }
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

fn write_simple(c: &mut Criterion) {
    test_write(c, 1, 1, 0, 0)
}

criterion_group!(write, write_simple);
criterion_main!(write);
