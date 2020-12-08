use mpsc::Receiver;
use tokio::sync::mpsc;

use mqtt3::{Client, PublishHandle};
use mqtt_broker_tests_util::client;
use mqtt_util::client_io::ClientIoSource;

use crate::{
    message_handler::{MessageHandler, RelayingMessageHandler, ReportResultMessageHandler},
    settings::{Settings, TestScenario},
    MessageTesterError, ShutdownHandle,
};

const EDGEHUB_CONTAINER_ADDRESS: &str = "edgehub:8883";

/// Abstracts the test logic for this generic mqtt telemetry test module.
/// This module is designed to test generic (non-iothub) mqtt telemetry in both a single-node and nested environment.
/// The module will run in one of two modes. The behavior depends on this mode.
///
/// 1: Initiate mode
/// - If nested scenario, test module runs on the lowest node in the topology.
/// - Spawn a thread that publishes messages continuously to upstream edge.
/// - Receives same message routed back from upstream edge and reports the result to the Test Result Coordinator test module.
///
/// 2: Relay mode
/// - If nested scenario, test module runs on the middle node in the topology.
/// - Receives a message from downstream edge and relays it back to downstream edge.
pub struct MessageTester {
    settings: Settings,
    client: Client<ClientIoSource>,
    publish_handle: PublishHandle,
    shutdown_handle: ShutdownHandle,
    shutdown_recv: Receiver<()>,
    message_handler: Box<dyn MessageHandler>,
}

impl MessageTester {
    pub fn new(settings: Settings) -> Result<Self, MessageTesterError> {
        let client = client::create_client_from_module_env(EDGEHUB_CONTAINER_ADDRESS.to_string())
            .map_err(MessageTesterError::ParseEnvironment)?;
        let publish_handle = client
            .publish_handle()
            .map_err(MessageTesterError::PublishHandle)?;

        let message_handler: Box<dyn MessageHandler> = match settings.test_scenario() {
            TestScenario::Initiate => Box::new(ReportResultMessageHandler::new()),
            TestScenario::Relay => Box::new(RelayingMessageHandler::new(publish_handle.clone())),
        };

        let (shutdown_send, shutdown_recv) = mpsc::channel::<()>(1);
        let shutdown_handle = ShutdownHandle::new(shutdown_send);

        Ok(Self {
            settings,
            client,
            publish_handle,
            shutdown_handle,
            message_handler,
            shutdown_recv,
        })
    }

    pub fn run() -> Result<(), MessageTesterError> {
        todo!()
    }

    pub fn shutdown_handle(&self) -> ShutdownHandle {
        self.shutdown_handle.clone()
    }
}
