#![deny(rust_2018_idioms)]
#![deny(clippy::all)]
#![allow(
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::missing_errors_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::shadow_unrelated,
    clippy::too_many_lines,
    clippy::type_complexity,
    clippy::use_self
)]

mod crypto;
mod module;

use config::{Config, File, FileFormat};
use crypto::{TestCrypto, TestKeyStore};
use edgelet_core::{
    CertificateIssuer, CertificateProperties, CertificateType, MakeModuleRuntime, ModuleAction,
};
use edgelet_http::CertificateManager;
use edgelet_test_utils::crypto::TestHsm;
use futures::sync::mpsc;
use futures::sync::oneshot;
use futures::sync::oneshot::{Receiver, Sender};
use futures::Future;
use iotedged::{workload::WorkloadData, workload_manager::WorkloadManager};
use module::{
    TestConfig, TestMod as TestModule, TestProvisioningResult, TestRuntime, TestSettings,
};
use serde_json::{self, json};

use std::path::Path;
use std::sync::Arc;
use std::thread;
use std::time;
use tempfile::tempdir;

const IOTEDGED_TLS_COMMONNAME: &str = "iotedged";
const TIME_FOR_CERT: u64 = 100;
const TIME_FOR_SERVER_UP: time::Duration = time::Duration::from_millis(100);

fn make_settings(workloadurl: &str, path: &Path) -> TestSettings {
    let mut config = Config::default();
    let config_json = json!({
        "listen": {
            "management_uri": "unix:///var/run/iotedge/mgmt.sock",
            "workload_uri": workloadurl
        },
        "homedir": path
    });

    config
        .merge(File::from_str(&config_json.to_string(), FileFormat::Json))
        .unwrap();

    config.try_into().unwrap()
}

#[test]
fn start_edgeagent_socket_succeeds() {
    let dir = tempdir().unwrap();
    let path = dir.path();

    let legacyworkloadpath = path.join("workload.sock");

    let legacyworkload = &legacyworkloadpath.into_os_string().into_string().unwrap();

    let mut unixprefix = "unix://".to_owned();
    unixprefix.push_str(&legacyworkload);

    let (create_socket_channel_snd, create_socket_channel_rcv) = mpsc::unbounded::<ModuleAction>();

    let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();

    let config = TestConfig::new("microsoft/test-image".to_string());
    let module: TestModule = TestModule::new("edgeAgent".to_string(), config);

    let settings = make_settings(&unixprefix, &path);
    let runtime = TestRuntime::make_runtime(
        settings.clone(),
        TestProvisioningResult::new(),
        TestHsm::default(),
        create_socket_channel_snd,
    )
    .wait()
    .unwrap()
    .with_module(module);

    let edgelet_cert_props = CertificateProperties::new(
        TIME_FOR_CERT,
        IOTEDGED_TLS_COMMONNAME.to_string(),
        CertificateType::Server,
        "iotedge-tls".to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);

    let crypto = TestCrypto {};
    let key_store = TestKeyStore {};
    let cert_manager = CertificateManager::new(crypto.clone(), edgelet_cert_props).unwrap();
    let cert_manager = Arc::new(cert_manager);

    let cfg = WorkloadData::new("iothub".to_string(), "dev_id".to_string(), 60, 60);

    WorkloadManager::start_manager::<TestRuntime>(
        &settings,
        &key_store,
        &runtime,
        &crypto,
        cfg,
        cert_manager,
        &mut tokio_runtime,
        create_socket_channel_rcv,
    )
    .unwrap();

    thread::sleep(TIME_FOR_SERVER_UP);

    let (sender, _receiver): (Sender<()>, Receiver<()>) = oneshot::channel();
    runtime
        .create_socket_channel
        .unbounded_send(ModuleAction::Start("edgeAgent".to_string(), sender))
        .unwrap();

    thread::sleep(TIME_FOR_SERVER_UP);

    if cfg!(windows) {
        let socketpath = path.join("mnt/edgeAgent");
        assert!(!socketpath.read_dir().unwrap().next().is_none());
    } else {
        let socketpath = path.join("mnt/edgeAgent.sock");
        assert!(socketpath.exists());
    }
}

#[test]
fn stop_edgeagent_workload_socket_fails() {
    let dir = tempdir().unwrap();
    let path = dir.path();

    let legacyworkloadpath = path.join("workload.sock");

    let legacyworkload = &legacyworkloadpath.into_os_string().into_string().unwrap();

    let mut unixprefix = "unix://".to_owned();
    unixprefix.push_str(&legacyworkload);

    let (create_socket_channel_snd, create_socket_channel_rcv) = mpsc::unbounded::<ModuleAction>();

    let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();

    let config = TestConfig::new("microsoft/test-image".to_string());
    let module: TestModule = TestModule::new("edgeAgent".to_string(), config);

    let settings = make_settings(&unixprefix, &path);
    let runtime = TestRuntime::make_runtime(
        settings.clone(),
        TestProvisioningResult::new(),
        TestHsm::default(),
        create_socket_channel_snd,
    )
    .wait()
    .unwrap()
    .with_module(module);

    let edgelet_cert_props = CertificateProperties::new(
        TIME_FOR_CERT,
        IOTEDGED_TLS_COMMONNAME.to_string(),
        CertificateType::Server,
        "iotedge-tls".to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);

    let crypto = TestCrypto {};
    let key_store = TestKeyStore {};
    let cert_manager = CertificateManager::new(crypto.clone(), edgelet_cert_props).unwrap();
    let cert_manager = Arc::new(cert_manager);

    let cfg = WorkloadData::new("iothub".to_string(), "dev_id".to_string(), 60, 60);

    WorkloadManager::start_manager::<TestRuntime>(
        &settings,
        &key_store,
        &runtime,
        &crypto,
        cfg,
        cert_manager,
        &mut tokio_runtime,
        create_socket_channel_rcv,
    )
    .unwrap();

    thread::sleep(TIME_FOR_SERVER_UP);
    runtime
        .create_socket_channel
        .unbounded_send(ModuleAction::Stop("edgeAgent".to_string()))
        .unwrap();
    thread::sleep(TIME_FOR_SERVER_UP);

    if cfg!(windows) {
        let socketpath = path.join("mnt/edgeAgent");
        assert!(!socketpath.read_dir().unwrap().next().is_none());
    } else {
        let socketpath = path.join("mnt/edgeAgent.sock");
        assert!(socketpath.exists());
    }
}

#[test]
fn start_workload_socket_succeeds() {
    let dir = tempdir().unwrap();
    let path = dir.path();

    let legacyworkloadpath = path.join("workload.sock");

    let legacyworkload = &legacyworkloadpath.into_os_string().into_string().unwrap();

    let mut unixprefix = "unix://".to_owned();
    unixprefix.push_str(&legacyworkload);

    let (create_socket_channel_snd, create_socket_channel_rcv) = mpsc::unbounded::<ModuleAction>();

    let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();

    let config = TestConfig::new("microsoft/test-image".to_string());
    let module: TestModule = TestModule::new("edgeAgent".to_string(), config);

    let settings = make_settings(&unixprefix, &path);
    let runtime = TestRuntime::make_runtime(
        settings.clone(),
        TestProvisioningResult::new(),
        TestHsm::default(),
        create_socket_channel_snd,
    )
    .wait()
    .unwrap()
    .with_module(module);

    let edgelet_cert_props = CertificateProperties::new(
        TIME_FOR_CERT,
        IOTEDGED_TLS_COMMONNAME.to_string(),
        CertificateType::Server,
        "iotedge-tls".to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);

    let crypto = TestCrypto {};
    let key_store = TestKeyStore {};
    let cert_manager = CertificateManager::new(crypto.clone(), edgelet_cert_props).unwrap();
    let cert_manager = Arc::new(cert_manager);

    let cfg = WorkloadData::new("iothub".to_string(), "dev_id".to_string(), 60, 60);

    WorkloadManager::start_manager::<TestRuntime>(
        &settings,
        &key_store,
        &runtime,
        &crypto,
        cfg,
        cert_manager,
        &mut tokio_runtime,
        create_socket_channel_rcv,
    )
    .unwrap();

    thread::sleep(TIME_FOR_SERVER_UP);
    let (sender, _receiver): (Sender<()>, Receiver<()>) = oneshot::channel();
    runtime
        .create_socket_channel
        .unbounded_send(ModuleAction::Start("test-agent".to_string(), sender))
        .unwrap();

    thread::sleep(TIME_FOR_SERVER_UP);

    if cfg!(windows) {
        let socketpath = path.join("mnt/test-agent");
        assert!(!socketpath.read_dir().unwrap().next().is_none());
    } else {
        let socketpath = path.join("mnt/test-agent.sock");
        assert!(socketpath.exists());
    }
}

#[test]
fn stop_workload_socket_succeeds() {
    let dir = tempdir().unwrap();
    let path = dir.path();

    let legacyworkloadpath = path.join("workload.sock");

    let legacyworkload = &legacyworkloadpath.into_os_string().into_string().unwrap();

    let mut unixprefix = "unix://".to_owned();
    unixprefix.push_str(&legacyworkload);

    let (create_socket_channel_snd, create_socket_channel_rcv) = mpsc::unbounded::<ModuleAction>();

    let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();

    let config = TestConfig::new("microsoft/test-image".to_string());
    let module: TestModule = TestModule::new("test-agent".to_string(), config);

    let settings = make_settings(&unixprefix, &path);
    let runtime = TestRuntime::make_runtime(
        settings.clone(),
        TestProvisioningResult::new(),
        TestHsm::default(),
        create_socket_channel_snd,
    )
    .wait()
    .unwrap()
    .with_module(module);

    let edgelet_cert_props = CertificateProperties::new(
        TIME_FOR_CERT,
        IOTEDGED_TLS_COMMONNAME.to_string(),
        CertificateType::Server,
        "iotedge-tls".to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);

    let crypto = TestCrypto {};
    let key_store = TestKeyStore {};
    let cert_manager = CertificateManager::new(crypto.clone(), edgelet_cert_props).unwrap();
    let cert_manager = Arc::new(cert_manager);

    let cfg = WorkloadData::new("iothub".to_string(), "dev_id".to_string(), 60, 60);

    WorkloadManager::start_manager::<TestRuntime>(
        &settings,
        &key_store,
        &runtime,
        &crypto,
        cfg,
        cert_manager,
        &mut tokio_runtime,
        create_socket_channel_rcv,
    )
    .unwrap();

    thread::sleep(TIME_FOR_SERVER_UP);

    let (sender, _receiver): (Sender<()>, Receiver<()>) = oneshot::channel();
    runtime
        .create_socket_channel
        .unbounded_send(ModuleAction::Start("test-agent".to_string(), sender))
        .unwrap();

    thread::sleep(TIME_FOR_SERVER_UP);

    runtime
        .create_socket_channel
        .unbounded_send(ModuleAction::Stop("test-agent".to_string()))
        .unwrap();
    thread::sleep(TIME_FOR_SERVER_UP);

    if cfg!(windows) {
        let socketpath = path.join("mnt/test-agent");
        assert!(socketpath.read_dir().unwrap().next().is_none());
    } else {
        let socketpath = path.join("mnt/test-agent.sock");
        assert!(!socketpath.exists());
    }
}
