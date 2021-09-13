use std::time::Duration;

use tokio::sync::mpsc;

mod deployment;
// mod reconcile;
mod util;

#[tokio::main]
async fn main() {
    // let (new_deployment_notifier, new_deployment_notifyee) = mpsc::channel(32);

    // let deployment_manager = deployment::DeploymentManager::new("/home/lee/test");
    // let reconcile_manager = reconcile::ReconcileManager::new(
    //     Duration::from_secs(10),
    //     new_deployment_notifyee,
    //     deployment_manager.file_location().into(),
    //     (),
    // );

    println!("Hello, world!");
}
