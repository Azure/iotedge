use fail::FailScenario;

use proptest::{collection::vec, prelude::*};
use tempfile::TempDir;

use mqtt_broker::{BrokerSnapshot, FilePersistor, Persist, PersistError, VersionedFileFormat};

const FAILPOINTS: &[&str] = &[
    "consolidatestate.load.deserialize_from",
    "consolidatestate.store.serialize_into",
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
    Store(BrokerSnapshot),
    AddFailpoint(&'static str),
    RemoveFailpoint(&'static str),
}

fn arb_op() -> impl Strategy<Value = Op> {
    prop_oneof![
        Just(Op::Load),
        Just(Op::Store(BrokerSnapshot::default())),
        proptest::sample::select(FAILPOINTS).prop_map(Op::AddFailpoint),
        proptest::sample::select(FAILPOINTS).prop_map(Op::RemoveFailpoint),
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
    let mut persistor =
        FilePersistor::new(path, VersionedFileFormat::default()).with_previous_count(count);

    // Make sure we've stored at least one state
    tear_down_failpoints();
    persistor.store(BrokerSnapshot::default()).await.unwrap();

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
    tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()
        .unwrap()
        .block_on(async {
            fail::cfg("filepersistor.load.spawn_blocking", "return").unwrap();

            let tmp_dir = TempDir::new().unwrap();
            let path = tmp_dir.path().to_owned();
            let mut persistor = FilePersistor::new(path, VersionedFileFormat::default());

            let result = persistor.load().await;
            matches::assert_matches!(result, Err(PersistError::TaskJoin(_)));
        });
    scenario.teardown();
}

// Generates random sequences of events and failures and ensures
// that the last committed snapshot isn't corrupted.
proptest! {
    #[test]
    fn test_failpoints(count in 0usize..10, ops in vec(arb_op(), 0..50)) {
        let scenario = FailScenario::setup();
        tokio::runtime::Builder::new_current_thread()
            .enable_all()
            .build()
            .unwrap()
            .block_on(test_persistor(count, ops));
        scenario.teardown();
    }
}
