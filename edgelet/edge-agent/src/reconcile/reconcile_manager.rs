use std::{path::PathBuf, time::Duration};

use edgelet_core::ModuleRuntime;
use edgelet_settings::DockerConfig;

use super::reconciler::Reconciler;
use crate::deployment::DeploymentProvider;

pub struct ReconcileManager<D, M> {
    frequency: Duration,
    reconciler: Reconciler<D, M>,
}

impl<D, M> ReconcileManager<D, M>
where
    D: DeploymentProvider + Send + Sync + 'static,
    M: ModuleRuntime<Config = DockerConfig> + Send + Sync + 'static,
{
    pub fn new(frequency: Duration, deployment_provider: D, runtime: M) -> Self {
        Self {
            frequency,
            reconciler: Reconciler::new(deployment_provider, runtime),
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
