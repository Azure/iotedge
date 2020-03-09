use fail::FailScenario;

use mqtt_broker::{BincodeFormat, BrokerState, ErrorKind, FilePersistor, Persist};
use proptest::collection::vec;
use proptest::prelude::*;
use tempfile::TempDir;

const FAILPOINTS: &[&str] = &[
    "bincodeformat.load.deserialize_from",
    "bincodeformat.store.serialize_into",
    "filepersistor.load.fileopen",
    "filepersistor.load.format",
    "filepersistor.load.spawn_blocking",
    "filepersistor.store.fileopen",
    "filepersistor.store.filerename",
    "filepersistor.store.symlink_unlink",
    "filepersistor.store.symlink",
    "filepersistor.store.createdir",
    "filepersistor.store.readdir",
    "filepersistor.store.entry_unlink",
    "filepersistor.store.new_file_unlink",
    "filepersistor.store.spawn_blocking",
];

#[derive(Clone, Debug)]
enum Op {
    Load,
    Store(BrokerState),
    AddFailpoint(&'static str),
    RemoveFailpoint(&'static str),
}

fn arb_op() -> impl Strategy<Value = Op> {
    prop_oneof![
        Just(Op::Load),
        Just(Op::Store(BrokerState::default())),
        proptest::sample::select(FAILPOINTS).prop_map(|f| Op::AddFailpoint(f)),
        proptest::sample::select(FAILPOINTS).prop_map(|f| Op::RemoveFailpoint(f)),
    ]
}

fn tear_down_failpoints() {
    for (name, _) in fail::list() {
        fail::remove(name);
    }
}

async fn test_persistor(count: usize, ops: Vec<Op>) {
    let tmp_dir = TempDir::new().unwrap();
    let path = tmp_dir.path().to_owned();
    let mut persistor = FilePersistor::new(path, BincodeFormat::new()).with_previous_count(count);

    // Make sure we've stored at least one state
    tear_down_failpoints();
    persistor.store(BrokerState::default()).await.unwrap();

    // process the operations
    for op in ops {
        match op {
            Op::Load => {
                let _ = persistor.load().await;
            }
            Op::Store(state) => {
                let _ = persistor.store(state).await;
            }
            Op::AddFailpoint(f) => fail::cfg(f, "return").unwrap(),
            Op::RemoveFailpoint(f) => fail::remove(f),
        }
    }

    // clear the failpoints and ensure we can load at least one state
    tear_down_failpoints();
    let state = persistor.load().await.unwrap();
    assert!(state.is_some());
}

#[test]
fn test_failpoints_smoketest() {
    let scenario = FailScenario::setup();
    tokio::runtime::Builder::new()
        .basic_scheduler()
        .enable_all()
        .build()
        .unwrap()
        .block_on(async {
            fail::cfg("filepersistor.load.spawn_blocking", "return").unwrap();

            let tmp_dir = TempDir::new().unwrap();
            let path = tmp_dir.path().to_owned();
            let mut persistor = FilePersistor::new(path, BincodeFormat::new());

            let result = persistor.load().await;
            let err = result.unwrap_err();
            assert_eq!(&ErrorKind::TaskJoin, err.kind());
        });
    scenario.teardown();
}

// Generates random sequences of events and failures and ensures
// that the last commited snapshot isn't corrupted.
proptest! {
    #[test]
    fn test_failpoints(count in 0usize..10, ops in vec(arb_op(), 0..50)) {
        let scenario = FailScenario::setup();
        tokio::runtime::Builder::new()
            .basic_scheduler()
            .enable_all()
            .build()
            .unwrap()
            .block_on(test_persistor(count, ops));
        scenario.teardown();
    }
}

#[test]
fn test_failpoints_regression1() {
    let scenario = FailScenario::setup();
    let ops = vec![
        Op::Store(BrokerState::default()),
        Op::Store(BrokerState::default()),
        Op::Store(BrokerState::default()),
        Op::RemoveFailpoint("filepersistor.store.symlink"),
        Op::AddFailpoint("filepersistor.store.readdir"),
        Op::RemoveFailpoint("filepersistor.store.createdir"),
        Op::Store(BrokerState::default()),
        Op::AddFailpoint("bincodeformat.store.serialize_into"),
        Op::RemoveFailpoint("filepersistor.store.readdir"),
        Op::Store(BrokerState::default()),
    ];

    tokio::runtime::Builder::new()
        .basic_scheduler()
        .enable_all()
        .build()
        .unwrap()
        .block_on(test_persistor(2, ops));
    scenario.teardown();
}

#[test]
fn test_failpoints_windows_failure() {
    let scenario = FailScenario::setup();
    let count = 0;
    let ops = vec![
        Op::Load,
        Op::AddFailpoint("filepersistor.store.filerename"),
        Op::Load,
        Op::AddFailpoint("bincodeformat.store.serialize_into"),
        Op::Store(BrokerState::default()),
        Op::Load,
        Op::AddFailpoint("bincodeformat.load.deserialize_from"),
        Op::AddFailpoint("filepersistor.load.spawn_blocking"),
    ];

    tokio::runtime::Builder::new()
        .basic_scheduler()
        .enable_all()
        .build()
        .unwrap()
        .block_on(test_persistor(count, ops));
    scenario.teardown();
}
