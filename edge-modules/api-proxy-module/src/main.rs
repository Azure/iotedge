#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms,
    clippy::must_use_candidate,
    clippy::missing_errors_doc,
    clippy::missing_panics_doc
)]
use std::{process::Stdio, sync::Arc};

use anyhow::{Context, Error, Result};
use futures_util::select;
use log::{error, info, warn, LevelFilter};
use tokio::{
    process::{Child, Command},
    sync::Notify,
    task::JoinHandle,
};

use api_proxy_module::{
    monitors::{certs_monitor, config_monitor},
    token_service::token_server,
    utils::{shutdown, shutdown_handle},
};
use shutdown_handle::ShutdownHandle;

#[tokio::main]
async fn main() -> Result<()> {
    env_logger::builder().filter_level(LevelFilter::Info).init();

    let notify_config_reload_api_proxy = Arc::new(Notify::new());
    let notify_server_cert_reload_api_proxy = Arc::new(Notify::new());
    let notify_trust_bundle_reload_api_proxy = Arc::new(Notify::new());

    let client = config_monitor::get_sdk_client()?;
    let mut shutdown_sdk = client
        .inner()
        .shutdown_handle()
        .context("Could not create Shutdown handle")?;

    let (config_monitor_handle, config_monitor_shutdown_handle) =
        config_monitor::start(client, notify_config_reload_api_proxy.clone())
            .context("Failed running config monitor")?;
    let (cert_monitor_handle, cert_monitor_shutdown_handle) = certs_monitor::start(
        notify_server_cert_reload_api_proxy.clone(),
        notify_trust_bundle_reload_api_proxy.clone(),
    )
    .context("Failed running certificates monitor")?;
    let (nginx_controller_handle, nginx_controller_shutdown_handle) = nginx_controller_start(
        notify_config_reload_api_proxy,
        notify_server_cert_reload_api_proxy,
        notify_trust_bundle_reload_api_proxy.clone(),
    )
    .context("Failed running nginx controller")?;
    let (token_server_handle, token_server_shutdown_handle) =
        token_server::start().context("Failed running token server")?;

    //If one task closes, clean up everything
    if let Err(e) = nginx_controller_handle.await {
        error!("Tasks encountered an error: {}", e);
    };

    //Send shutdown signal to all task
    shutdown_sdk
        .shutdown()
        .await
        .context("Fatal, could not shut down SDK")?;

    cert_monitor_shutdown_handle.shutdown().await;
    config_monitor_shutdown_handle.shutdown().await;
    nginx_controller_shutdown_handle.shutdown().await;
    token_server_shutdown_handle.shutdown().await;

    if let Err(e) = cert_monitor_handle.await {
        error!("error on finishing cert monitor: {}", e);
    }
    if let Err(e) = config_monitor_handle.await {
        error!("error on finishing config monitor: {}", e);
    }
    if let Err(e) = token_server_handle.await {
        error!("error on finishing config monitor: {}", e);
    }

    info!("Api proxy stopped");
    Ok(())
}

enum NginxCommands {
    Restart,
    Stop,
    Reload,
}

pub fn nginx_controller_start(
    notify_config_reload_api_proxy: Arc<Notify>,
    notify_server_cert_reload_api_proxy: Arc<Notify>,
    notify_trust_bundle_reload_api_proxy: Arc<Notify>,
) -> Result<(JoinHandle<Result<()>>, ShutdownHandle), Error> {
    let program_path = "/usr/sbin/nginx";
    let proxy_name = "nginx";
    let stop_proxy_args = vec!["-s".to_string(), "stop".to_string()];
    let reload_proxy_args = vec!["-s".to_string(), "reload".to_string()];
    let start_proxy_args = vec![
        "-c".to_string(),
        "/app/nginx_config.conf".to_string(),
        "-g".to_string(),
        "daemon off;".to_string(),
    ];

    let shutdown_signal = Arc::new(Notify::new());
    let shutdown_handle = ShutdownHandle(shutdown_signal.clone());

    let monitor_loop: JoinHandle<Result<()>> = tokio::spawn(async move {
        use futures_util::FutureExt;

        //This is just to avoid error at the beginning when nginx tries to start
        //Wait for configuration to be ready.
        notify_config_reload_api_proxy.notified().await;

        //Wait for the trust bundle.
        notify_trust_bundle_reload_api_proxy.notified().await;

        //Wait for the server cert and private key.
        notify_server_cert_reload_api_proxy.notified().await;

        //Start nginx
        loop {
            let nginx_start = nginx_command(proxy_name, program_path, &start_proxy_args, "start")?;
            futures_util::pin_mut!(nginx_start);
            info!("Starting/Restarting API-Proxy");

            loop {
                let nginx_start = nginx_start.wait().fuse();

                //Shutdown nginx on ctrl_c or signal
                let wait_shutdown_ctrl_c = shutdown::shutdown().fuse();
                let wait_shutdown_signal = shutdown_signal.notified().fuse();

                // Restart nginx on new config, new cert or crash.
                let cert_reload = notify_server_cert_reload_api_proxy.notified().fuse();
                let config_reload = notify_config_reload_api_proxy.notified().fuse();

                futures_util::pin_mut!(
                    nginx_start,
                    wait_shutdown_ctrl_c,
                    wait_shutdown_signal,
                    cert_reload,
                    config_reload
                );

                // Bug in clippy, not using mut mut here
                #[allow(clippy::mut_mut)]
                let proxy_command = select! {
                    _ = wait_shutdown_ctrl_c => NginxCommands::Stop,
                    _ = wait_shutdown_signal => NginxCommands::Stop,
                    _ = cert_reload => NginxCommands::Reload,
                    _ = config_reload => NginxCommands::Reload,
                    _ = nginx_start => NginxCommands::Restart,
                };

                match proxy_command {
                    NginxCommands::Restart => {
                        // Stop nginx and restart nginx
                        info!("Request to restart Nginx received");
                        nginx_command(proxy_name, program_path, &stop_proxy_args, "stop")?
                            .wait()
                            .await
                            .context("Error running the command")?;

                        //Break will exit the inner loop and restart nginx
                        break;
                    }
                    NginxCommands::Reload => {
                        // Reload nginx config
                        info!("Request to reload Nginx received");
                        nginx_command(proxy_name, program_path, &reload_proxy_args, "reload")?
                            .wait()
                            .await
                            .context("Error running the command")?;
                    }
                    NginxCommands::Stop => {
                        // Closes everything
                        warn!("Shutting down ngxing controller!");
                        return Ok(());
                    }
                }
            }
        }
    });

    Ok((monitor_loop, shutdown_handle))
}

fn nginx_command(
    proxy_name: &str,
    program_path: &str,
    proxy_command: &[String],
    command_name: &str,
) -> Result<Child, Error> {
    Command::new(program_path)
        .args(proxy_command)
        .stdout(Stdio::inherit())
        .spawn()
        .with_context(|| format!("Failed to {}  {}", command_name, proxy_name))
        .context("Cannot execute command")
}
