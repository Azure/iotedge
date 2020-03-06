use fail::FailScenario;

use mqtt_broker::{BincodeFormat, BrokerState, FilePersistor, Persist};
use proptest::collection::vec;
use proptest::prelude::*;
use tempfile::TempDir;

const FAILPOINTS: &'static [&'static str] = &[
    "filepersistor.load.fileopen",
    "filepersistor.load.format",
    "filepersistor.load.spawn_blocking",
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

#[test]
fn test_failpoints_smoketest() {
    tokio::runtime::Builder::new()
        .basic_scheduler()
        .enable_all()
        .build()
        .unwrap()
        .block_on(async {
            let scenario = FailScenario::setup();
            fail::cfg("filepersistor.load.spawn_blocking", "return").unwrap();

            let tmp_dir = TempDir::new().unwrap();
            let path = tmp_dir.path().to_owned();
            let mut persistor = FilePersistor::new(path, BincodeFormat::new());

            let result = persistor.load().await;
            assert!(result.is_err());
            scenario.teardown();
        })
}

proptest! {
    #[test]
    fn test_failpoints(ops in vec(arb_op(), 0..20)) {
        tokio::runtime::Builder::new()
            .basic_scheduler()
            .enable_all()
            .build()
            .unwrap()
            .block_on(async {
                let scenario = FailScenario::setup();
                let tmp_dir = TempDir::new().unwrap();
                let path = tmp_dir.path().to_owned();
                let mut persistor = FilePersistor::new(path, BincodeFormat::new());

                // Make sure we've stored at least one state
                persistor.store(BrokerState::default()).await.unwrap();

                // process the operations
                for op in ops {
                    match op {
                        Op::Load => { let _ = persistor.load().await; },
                        Op::Store(state) => { let _ = persistor.store(state).await; },
                        Op::AddFailpoint(f) => fail::cfg(f, "return").unwrap(),
                        Op::RemoveFailpoint(f) => fail::remove(f),
                    }
                }

                // clear the failpoints and ensure we can load at least one state
                tear_down_failpoints();
                let state = persistor.load().await.unwrap();
                assert!(state.is_some());
                scenario.teardown();
            })
    }
}
