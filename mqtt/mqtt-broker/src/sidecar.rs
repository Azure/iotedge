use std::{error::Error as StdError, future::Future, pin::Pin};

use async_trait::async_trait;

/// A common trait for any additional routine that enriches MQTT broker behavior.
#[async_trait]
pub trait Sidecar {
    /// Returns a new instance of a shutdown handle to be used to stop sidecar.
    fn shutdown_handle(&self) -> Result<SidecarShutdownHandle, SidecarShutdownHandleError>;

    /// Starts a routine.
    async fn run(self: Box<Self>);
}

/// Shutdown handle to request a sidecar to stop.
pub struct SidecarShutdownHandle(Pin<Box<dyn Future<Output = ()> + Send>>);

impl SidecarShutdownHandle {
    pub fn new<F>(shutdown: F) -> Self
    where
        F: Future<Output = ()> + Send + 'static,
    {
        Self(Box::pin(shutdown))
    }

    pub async fn shutdown(self) {
        self.0.await
    }
}

/// This error returned when there is impossible to obtain a shutdown handle.
#[derive(Debug, thiserror::Error)]
#[error("unable to obtain shutdown handler for sidecar. {0}")]
pub struct SidecarShutdownHandleError(#[source] pub Box<dyn StdError + Send + Sync>);
