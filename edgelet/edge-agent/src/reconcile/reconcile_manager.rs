use std::{path::PathBuf, time::Duration};

use tokio::{select, sync::mpsc};

use edgelet_core::ModuleRuntime;

use super::reconciler::Reconciler;

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
