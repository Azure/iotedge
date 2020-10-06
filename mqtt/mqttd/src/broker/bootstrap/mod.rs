use futures_util::future::select_all;
use tokio::task::JoinHandle;

use tracing::error;

#[cfg(feature = "edgehub")]
mod edgehub;

#[cfg(feature = "edgehub")]
pub use edgehub::{
    broker, config, start_server, start_sidecars, SidecarError, SidecarShutdownHandle,
};

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
mod generic;

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
pub use generic::{
    broker, config, start_server, start_sidecars, SidecarError, SidecarShutdownHandle,
};

/// Wraps join handles for sidecar processes and exposes single future
/// Exposed future will wait for any sidecar to complete, then shut down the rest
/// Also exposes shutdown handle used to shut down all the sidecars
pub struct SidecarManager {
    join_handles: Vec<JoinHandle<()>>,
    shutdown_handle: SidecarShutdownHandle,
}

impl SidecarManager {
    #![allow(dead_code)] // needed because we have no sidecars for the generic feature
    pub fn new(join_handles: Vec<JoinHandle<()>>, shutdown_handle: SidecarShutdownHandle) -> Self {
        Self {
            join_handles,
            shutdown_handle,
        }
    }

    pub async fn wait_for_shutdown(self) -> Result<(), SidecarError> {
        let (sidecar_output, _, other_handles) = select_all(self.join_handles).await;

        if let Err(e) = sidecar_output {
            error!(message = "failed waiting for sidecar shutdown", err = %e);
        }

        self.shutdown_handle.shutdown().await?;

        for handle in other_handles {
            if let Err(e) = handle.await {
                error!(message = "failed waiting for sidecar shutdown", err = %e);
            }
        }

        Ok(())
    }

    pub fn shutdown_handle(&self) -> SidecarShutdownHandle {
        self.shutdown_handle.clone()
    }
}
