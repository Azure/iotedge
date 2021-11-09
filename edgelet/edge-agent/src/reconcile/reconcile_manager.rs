use std::{path::PathBuf, time::Duration};

use edgelet_core::ModuleRuntime;
use edgelet_settings::DockerConfig;

use super::reconciler::Reconciler;

pub struct ReconcileManager<M> {
    frequency: Duration,
    reconciler: Reconciler<M>,
}

impl<M> ReconcileManager<M>
where
    M: ModuleRuntime<Config = DockerConfig> + Send + Sync + 'static,
{
    pub fn new(frequency: Duration, deployment_file: PathBuf, runtime: M) -> Self {
        Self {
            frequency,
            reconciler: Reconciler::new(deployment_file, runtime),
        }
    }

    pub fn start(self) {
        tokio::spawn(async move {
            println!("Started task Reconciliation");

            loop {
                tokio::time::sleep(self.frequency).await;
                println!("Starting Periodic Reconciliation");
                self.reconciler.reconcile().await;
            }
        });
    }
}
