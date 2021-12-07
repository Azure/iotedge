use std::{sync::Arc, time::Duration};

use tokio::sync::Mutex;

use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_settings::DockerConfig;

use super::reconciler::Reconciler;
use crate::deployment::DeploymentProvider;

pub struct ReconcileManager<D, M, R> {
    frequency: Duration,
    reconciler: Reconciler<D, M, R>,
}

impl<D, M, R> ReconcileManager<D, M, R>
where
    D: DeploymentProvider + Send + Sync + 'static,
    M: ModuleRuntime<Config = DockerConfig> + Send + Sync + 'static,
    R: ModuleRegistry<Config = DockerConfig> + Send + Sync + 'static,
{
    pub fn new(
        frequency: Duration,
        deployment_provider: Arc<Mutex<D>>,
        runtime: M,
        registry: R,
    ) -> Self {
        Self {
            frequency,
            reconciler: Reconciler::new(deployment_provider, runtime, registry),
        }
    }

    pub fn start(self) {
        let Self {
            frequency,
            mut reconciler,
        } = self;

        tokio::spawn(async move {
            println!("Started task Reconciliation");

            loop {
                tokio::time::sleep(frequency).await;
                println!("Starting Periodic Reconciliation");
                if let Err(error) = reconciler.reconcile().await {
                    println!("Error while reconciling: {:#?}", error);
                }
            }
        });
    }
}
