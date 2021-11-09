mod deployment;
mod reconcile;
mod hub_client_manager;
mod util;

use std::sync::Arc;

use aziot_cert_client_async::Client as CertClient;
use aziot_identity_client_async::Client as IdentityClient;
use aziot_key_client_async::Client as KeyClient;
use edgelet_settings::{docker::Settings, RuntimeSettings};
use tokio::sync::Mutex;

pub async fn start_edgeagent(
    settings: &Settings,
    device_info: &aziot_identity_common::AzureIoTSpec,
    cert_client: Arc<CertClient>,
    key_client: Arc<KeyClient>,
    identity_client: Arc<IdentityClient>,
    mut shutdown_rx: tokio::sync::mpsc::UnboundedReceiver<edgelet_core::ShutdownReason>,
) -> Result<(), Box<dyn std::error::Error>> {
    println!("Starting EdgeAgent");
    let deployment_manager = Arc::new(Mutex::new(
        deployment::DeploymentManager::new("storage_location").await?,
    ));
    let client_manager = hub_client_manager::ClientManager::new(
        settings.hostname().to_owned(),
        &device_info.device_id.0,
        settings.trust_bundle_cert().ok_or_else(|| {
            std::io::Error::new(std::io::ErrorKind::Other, "missing trust bundle cert")
        })?,
        cert_client,
        key_client,
        identity_client,
        deployment_manager.clone(),
    )
    .await?;

    client_manager.start();

    println!("Started EdgeAgent");
    shutdown_rx.recv().await;
    Ok(())
}
