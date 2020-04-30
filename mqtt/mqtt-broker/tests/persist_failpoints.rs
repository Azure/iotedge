use fail::FailScenario;

use mqtt_broker::{BrokerState, ConsolidatedStateFormat, FilePersistor, Persist, PersistError};
use proptest::collection::vec;
use proptest::prelude::*;
use tempfile::TempDir;

const FAILPOINTS: &[&str] = &[
    "consolidatestate.load.deserialize_from",
    "consolidatestate.store.serialize_into",
    "filepersistor.load.fileopen",
    "filepersistor.load.format",
    "filepersistor.store.fileopen",
    "filepersistor.store.filerename",
    "filepersistor.store.symlink_unlink",
    "filepersistor.store.symlink",
    "filepersistor.store.createdir",
    "filepersistor.store.readdir",
    "filepersistor.store.entry_unlink",
    "filepersistor.store.new_file_unlink",
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

fn test_persistor(count: usize, ops: Vec<Op>) {
    let tmp_dir = TempDir::new().unwrap();
    let path = tmp_dir.path().to_owned();
    let mut persistor =
        FilePersistor::new(path, ConsolidatedStateFormat::default()).with_previous_count(count);

    // Make sure we've stored at least one state
    tear_down_failpoints();
    persistor.store(BrokerState::default()).unwrap();

    // process the operations
    for op in ops {
        match op {
            Op::Load => {
                let _ = persistor.load();
            }
            Op::Store(state) => {
                let _ = persistor.store(state);
            }
            Op::AddFailpoint(f) => fail::cfg(f, "return").unwrap(),
            Op::RemoveFailpoint(f) => fail::remove(f),
        }
    }

    // clear the failpoints and ensure we can load at least one state
    tear_down_failpoints();
    let state = persistor.load().unwrap();
    assert!(state.is_some());
}

// This test is meant to verify that the failpoints are actually enabled.
// This is to prevent the case where someone disables the `fail/failpoints` feature
// in the `Config.yaml` and unknowingly turns off the failpoints. This would render
// the proptest tests useless because they would not exercise the failpoints.
//
// This smoke test ensures that the failpoints are actually enabled and should
// catch this mistake.
#[test]
fn test_failpoints_smoketest() {
    let scenario = FailScenario::setup();

    fail::cfg("filepersistor.store.fileopen", "return").unwrap();

    let tmp_dir = TempDir::new().unwrap();
    let path = tmp_dir.path().to_owned();
    let mut persistor = FilePersistor::new(path, ConsolidatedStateFormat::default());

    let result = persistor.store(BrokerState::default());
    matches::assert_matches!(result, Err(PersistError::FileOpen(_, _)));

    scenario.teardown();
}

// Generates random sequences of events and failures and ensures
// that the last commited snapshot isn't corrupted.
proptest! {
    #[test]
    fn test_failpoints(count in 0usize..10, ops in vec(arb_op(), 0..50)) {
        let scenario = FailScenario::setup();
        test_persistor(count, ops);
        scenario.teardown();
    }
}
