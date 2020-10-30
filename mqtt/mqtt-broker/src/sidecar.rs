use std::{future::Future, pin::Pin};

use async_trait::async_trait;
use futures_util::FutureExt;
use tokio::sync::mpsc::{self, Receiver, Sender};

/// A common trait for any additional routine that enriches MQTT broker behavior.
#[async_trait]
pub trait Sidecar {
    /// Returns a new instance of a shutdown handle to be used to stop sidecar.
    fn shutdown_handle(&self) -> Result<SidecarShutdownHandle, SidecarShutdownHandleError>;

    /// Starts a routine.
    async fn run(self: Box<Self>);
}

/// Shutdown handle to request a sidecar to stop.
pub struct SidecarShutdownHandle(Pin<Box<dyn Future<Output = ()>>>);

impl SidecarShutdownHandle {
    pub fn new<F>(shutdown: F) -> Self
    where
        F: Future<Output = ()> + 'static,
    {
        Self(Box::pin(shutdown))
    }

    pub async fn shutdown(self) {
        self.0.await
    }
}

/// This error returned when there is impossible to obtain a shutdown handle.
#[derive(Debug, thiserror::Error)]
#[error("unable to obtain shutdown handler for sidecar")]
pub struct SidecarShutdownHandleError;

/// Creates a new instance of `PendingSidecar`.
pub fn pending() -> PendingSidecar {
    let (rx, tx) = mpsc::channel(1);
    PendingSidecar(rx, tx)
}

/// A stub sidecar which does not do any work and just waits for shutdown signal.
pub struct PendingSidecar(Sender<()>, Receiver<()>);

#[async_trait]
impl Sidecar for PendingSidecar {
    fn shutdown_handle(&self) -> Result<SidecarShutdownHandle, SidecarShutdownHandleError> {
        let mut handle = self.0.clone();
        let shutdown = async move {
            handle.send(()).map(drop).await;
        };
        Ok(SidecarShutdownHandle::new(shutdown))
    }

    async fn run(mut self: Box<Self>) {
        self.1.recv().await;
    }
}
