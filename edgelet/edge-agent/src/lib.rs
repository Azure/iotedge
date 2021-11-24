// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::missing_errors_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self,
    clippy::option_if_let_else
)]

mod deployment;
mod hub_client_manager;
mod reconcile;

use std::sync::Arc;
use std::time::Duration;

use aziot_cert_client_async::Client as CertClient;
use aziot_identity_client_async::Client as IdentityClient;
use aziot_key_client_async::Client as KeyClient;
use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_settings::DockerConfig;
use edgelet_settings::{docker::Settings, RuntimeSettings};
use tokio::sync::Mutex;

#[allow(clippy::too_many_arguments)]
pub async fn start_edgeagent<M, R>(
    settings: &Settings,
    device_info: &aziot_identity_common::AzureIoTSpec,
    cert_client: Arc<CertClient>,
    key_client: Arc<KeyClient>,
    identity_client: Arc<IdentityClient>,
    mut shutdown_rx: tokio::sync::mpsc::UnboundedReceiver<edgelet_core::ShutdownReason>,
    runtime: M,
    registry: R,
) -> Result<(), Box<dyn std::error::Error + Send + Sync>>
where
    M: ModuleRuntime<Config = DockerConfig> + Send + Sync + 'static,
    R: ModuleRegistry<Config = DockerConfig> + Send + Sync + 'static,
{
    println!("Starting EdgeAgent");
    let deployment_manager = Arc::new(Mutex::new(
        deployment::DeploymentManager::new("storage_location").await?,
    ));
    let client_manager = hub_client_manager::ClientManager::new(
        settings.hostname().to_owned(), // TODO: Get iothub hostname
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
    let reconcile_manager = reconcile::ReconcileManager::new(
        Duration::from_secs(5),
        deployment_manager.clone(),
        runtime,
        registry,
    );

    client_manager.start();
    reconcile_manager.start();

    println!("Started EdgeAgent");
    shutdown_rx.recv().await;
    Ok(())
}
