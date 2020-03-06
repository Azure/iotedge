use fail::FailScenario;

use mqtt_broker::{BincodeFormat, FilePersistor, Persist};
use tempfile::TempDir;

#[tokio::test]
async fn test_failpoints() {
    let scenario = FailScenario::setup();
    fail::cfg("filepersistor.load.spawn_blocking", "return").unwrap();

    let tmp_dir = TempDir::new().unwrap();
    let path = tmp_dir.path().to_owned();
    let mut persistor = FilePersistor::new(path, BincodeFormat::new());

    let result = persistor.load().await;
    assert!(result.is_err());
    scenario.teardown();
}

