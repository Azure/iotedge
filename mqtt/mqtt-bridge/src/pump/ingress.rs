use mockall_double::double;
use tracing::{error, info};

use crate::client::{MqttClient, MqttEventHandler};

#[double]
use crate::client::ShutdownHandle;

/// Handles incoming MQTT publications and puts them into the store.
pub(crate) struct Ingress<H> {
    client: MqttClient<H>,
    shutdown_client: Option<ShutdownHandle>,
}

impl<H> Ingress<H>
where
    H: MqttEventHandler,
{
    /// Creates a new instance of ingress.
    pub(crate) fn new(client: MqttClient<H>, shutdown_client: ShutdownHandle) -> Self {
        Self {
            client,
            shutdown_client: Some(shutdown_client),
        }
    }

    /// Returns a shutdown handle of ingress.
    pub(crate) fn handle(&mut self) -> IngressShutdownHandle {
        IngressShutdownHandle(self.shutdown_client.take())
    }

    /// Runs ingress processing.
    pub(crate) async fn run(mut self) -> Result<(), IngressError> {
        info!("starting ingress publication processing...");
        self.client.run().await?;
        info!("ingress publication processing stopped");

        Ok(())
    }
}

/// Ingress shutdown handle.
pub(crate) struct IngressShutdownHandle(Option<ShutdownHandle>);

impl IngressShutdownHandle {
    /// Sends a signal to shutdown ingress.
    pub(crate) async fn shutdown(mut self) {
        if let Some(mut sender) = self.0.take() {
            if let Err(e) = sender.shutdown().await {
                error!("unable to request shutdown for ingress. {}", e);
            }
        }
    }
}

#[derive(Debug, thiserror::Error)]
pub(crate) enum IngressError {
    #[error("mqtt client error. Caused by: {0}")]
    MqttClient(#[from] crate::client::ClientError),
}
