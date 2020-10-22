use tracing::{error, info};

use crate::client::{ClientShutdownHandle, EventHandler, MqttClient};

/// Handles incoming MQTT publications and puts them into the store.
pub(crate) struct Ingress<H> {
    client: MqttClient<H>,
    shutdown_client: Option<ClientShutdownHandle>,
}

impl<H> Ingress<H>
where
    H: EventHandler,
{
    /// Creates a new instance of ingress.
    pub(crate) fn new(client: MqttClient<H>, shutdown_client: ClientShutdownHandle) -> Self {
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
    pub(crate) async fn run(mut self) {
        info!("starting ingress publication processing...",);
        self.client.handle_events().await;
        info!("finished ingress publication processing");
    }
}

/// Ingress shutdown handle.
pub(crate) struct IngressShutdownHandle(Option<ClientShutdownHandle>);

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
