use futures_util::future::select_all;
use tokio::task::JoinHandle;

use tracing::error;

#[cfg(feature = "edgehub")]
mod edgehub;

#[cfg(feature = "edgehub")]
pub use edgehub::{broker, config, start_server, start_sidecars, SidecarShutdownHandle};

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
mod generic;

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
pub use generic::{broker, config, start_server, start_sidecars, SidecarShutdownHandle};

pub struct SidecarManager {
    join_handles: Vec<JoinHandle<()>>,
    shutdown_handle: SidecarShutdownHandle,
}

impl SidecarManager {
    pub fn new(join_handles: Vec<JoinHandle<()>>, shutdown_handle: SidecarShutdownHandle) -> Self {
        Self {
            join_handles,
            shutdown_handle,
        }
    }

    pub async fn wait_for_shutdown(self) {
        let (sidecar_output, _, other_handles) = select_all(self.join_handles).await;

        // wait for sidecars to finish
        if let Err(e) = sidecar_output {
            error!(message = "failed waiting for sidecar shutdown", err = %e);
        }
        for handle in other_handles {
            if let Err(e) = handle.await {
                error!(message = "failed waiting for sidecar shutdown", err = %e);
            }
        }
    }

    pub fn shutdown_handle(&self) -> SidecarShutdownHandle {
        self.shutdown_handle.clone()
    }
}
