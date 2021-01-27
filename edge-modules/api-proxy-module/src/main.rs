#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms,
    clippy::must_use_candidate,
    clippy::missing_errors_doc
)]
use std::{process::Stdio, sync::Arc};

use anyhow::{Context, Error, Result};
use futures_util::future::{self, Either};
use log::{error, info, warn, LevelFilter};
use tokio::{process::Command, sync::Notify, task::JoinHandle};

use api_proxy_module::{
    monitors::{certs_monitor, config_monitor, shutdown_handle},
    signals::shutdown,
};
use shutdown_handle::ShutdownHandle;

#[tokio::main]
async fn main() -> Result<()> {
    env_logger::builder().filter_level(LevelFilter::Info).init();

    let notify_config_reload_api_proxy = Arc::new(Notify::new());
    let notify_cert_reload_api_proxy = Arc::new(Notify::new());

    let client = config_monitor::get_sdk_client()?;
    let mut shutdown_sdk = client
        .inner()
        .shutdown_handle()
        .context("Could not create Shutdown handle")?;

    let (config_monitor_handle, config_monitor_shutdown_handle) =
        config_monitor::start(client, notify_config_reload_api_proxy.clone())
            .context("Failed running config monitor")?;
    let (cert_monitor_handle, cert_monitor_shutdown_handle) =
        certs_monitor::start(notify_cert_reload_api_proxy.clone())
            .context("Failed running certificates monitor")?;
    let (nginx_controller_handle, nginx_controller_shutdown_handle) =
        nginx_controller_start(notify_config_reload_api_proxy, notify_cert_reload_api_proxy)
            .context("Failed running nginx controller")?;

    //If one task closes, clean up everything
    if let Err(e) = nginx_controller_handle.await {
        error!("Tasks encountered and error {}", e);
    };

    //Send shutdown signal to all task
    shutdown_sdk
        .shutdown()
        .await
        .context("Fatal, could not shut down SDK")?;

    cert_monitor_shutdown_handle.shutdown().await;
    config_monitor_shutdown_handle.shutdown().await;
    nginx_controller_shutdown_handle.shutdown().await;

    if let Err(e) = cert_monitor_handle.await {
        error!("error on finishing cert monitor: {}", e);
    }
    if let Err(e) = config_monitor_handle.await {
        error!("error on finishing config monitor: {}", e);
    }

    info!("Api proxy stopped");
    Ok(())
}

pub fn nginx_controller_start(
    notify_config_reload_api_proxy: Arc<Notify>,
    notify_cert_reload_api_proxy: Arc<Notify>,
) -> Result<(JoinHandle<Result<()>>, ShutdownHandle), Error> {
    let program_path = "/usr/sbin/nginx";
    let args = vec![
        "-c".to_string(),
        "/app/nginx_config.conf".to_string(),
        "-g".to_string(),
        "daemon off;".to_string(),
    ];
    let name = "nginx";
    let stop_proxy_name = "stop nginx";
    let stop_proxy_program_path = "nginx";
    let stop_proxy_args = vec!["-s".to_string(), "stop".to_string()];

    let shutdown_signal = Arc::new(Notify::new());
    let shutdown_handle = ShutdownHandle(shutdown_signal.clone());

    let monitor_loop: JoinHandle<Result<()>> = tokio::spawn(async move {
        //This is just to avoid error at the beginning when nginx tries to start
        //Wait for configuration to be ready.
        notify_config_reload_api_proxy.notified().await;

        //Wait for the trust bundle.
        notify_cert_reload_api_proxy.notified().await;

        //Wait for the server cert and private key.
        notify_cert_reload_api_proxy.notified().await;

        loop {
            //Start nginx
            let child_nginx = Command::new(program_path)
                .args(&args)
                .stdout(Stdio::inherit())
                .spawn()
                .with_context(|| format!("Failed to start {} process.", name))
                .context("Cannot start proxy!")?;

            // Restart nginx on new config, new cert or crash.
            let cert_reload = notify_cert_reload_api_proxy.notified();
            let config_reload = notify_config_reload_api_proxy.notified();
            futures::pin_mut!(cert_reload, config_reload);
            let signal_restart_nginx = future::select(cert_reload, config_reload);
            futures::pin_mut!(child_nginx, signal_restart_nginx);
            let restart_proxy = future::select(child_nginx, signal_restart_nginx);

            //Shutdown on ctrl+c or on signal
            let wait_shutdown_ctrl_c = shutdown::shutdown();
            futures::pin_mut!(wait_shutdown_ctrl_c);
            let wait_shutdown_signal = shutdown_signal.notified();
            futures::pin_mut!(wait_shutdown_signal);
            let wait_shutdown = future::select(wait_shutdown_ctrl_c, wait_shutdown_signal);

            info!("Starting/Restarting API-Proxy");
            match future::select(wait_shutdown, restart_proxy).await {
                Either::Left(_) => {
                    warn!("Shutting down ngxing controller!");
                    return Ok(());
                }
                Either::Right((result, _)) => {
                    match result {
                        Either::Left(_) => {
                            info!("Nginx crashed, restarting");
                        }
                        Either::Right(_) => {
                            info!("Request to restart Nginx received");
                        }
                    };
                }
            }

            //Make sure proxy is stopped by sending stop command. Otherwise port will be blocked
            let command = Command::new(stop_proxy_program_path)
                .args(&stop_proxy_args)
                .spawn()
                .with_context(|| format!("Failed to start {} process.", stop_proxy_name))
                .context("Cannot stop proxy!")?;
            command
                .await
                .context("Error while trying to wait on stop proxy future")?;
        }
    });

    Ok((monitor_loop, shutdown_handle))
}
//add pin utils
