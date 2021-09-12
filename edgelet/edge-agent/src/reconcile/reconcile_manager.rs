use std::{error::Error, path::PathBuf, time::Duration};

use tokio::{io::AsyncReadExt, select, sync::mpsc};

use edgelet_core::ModuleRuntime;

use crate::deployment::{deployment::Deployment, DeploymentManager};

pub struct ReconcileManager<M> {
    frequency: Duration,
    twin_update_notifier: mpsc::Receiver<()>,
    reconciler: Reconciler<M>,
}

impl<M> ReconcileManager<M>
where
    M: ModuleRuntime + Send + Sync + 'static,
{
    pub fn new(
        frequency: Duration,
        twin_update_notifier: mpsc::Receiver<()>,
        deployment_file: PathBuf,
        runtime: M,
    ) -> Self {
        Self {
            frequency,
            twin_update_notifier,
            reconciler: Reconciler::new(deployment_file, runtime),
        }
    }

    pub fn start(self) {
        tokio::spawn(async move {
            println!("Started task Reconciliation");

            let Self {
                mut twin_update_notifier,
                frequency,
                reconciler,
            } = self;

            loop {
                select! {
                    () = async { tokio::time::sleep(frequency).await; } => {
                        println!("Starting Periodic Reconciliation");
                        reconciler.reconcile().await;
                    }
                    val = async { twin_update_notifier.recv().await } => if let Some(_) = val {
                        println!("Twin update received, starting reconciliation");
                        reconciler.reconcile().await;
                    } else {
                        println!("Twin update channel closed, ending reconciliation");
                        break;
                    },
                }
            }
        });
    }
}

struct Reconciler<M> {
    deployment_file: PathBuf,
    runtime: M,
}

impl<M> Reconciler<M>
where
    M: ModuleRuntime,
{
    fn new(deployment_file: PathBuf, runtime: M) -> Self {
        Self {
            deployment_file,
            runtime,
        }
    }

    async fn reconcile(&self) {
        println!("Find diff of stuff")
    }

    async fn get_expected_modules(&self) -> Result<Vec<Module>, Box<dyn Error>> {
        let deployment = DeploymentManager::get_deployment(&self.deployment_file).await;

        Ok(vec![])
    }

    async fn get_current_modules(&self) -> Result<Vec<Module>, Box<dyn Error>> {
        Ok(vec![])
    }
}

struct Module {}
