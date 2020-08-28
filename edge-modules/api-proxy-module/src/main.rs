//#![deny(rust_2018_idioms, warnings)]
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
mod monitors;
mod signals;
use anyhow::{Context, Error, Result};
use futures_util::future::{self, Either};
use monitors::certs_monitor;
use monitors::config_monitor;
use monitors::utils;
use std::process::Stdio;
use std::sync::Arc;
use tokio::process::Command;
use tokio::sync::Notify;
use tokio::task::JoinHandle;
use utils::ShutdownHandle;

#[tokio::main]
async fn main() -> Result<()> {
    env_logger::Builder::from_env(
        env_logger::Env::new().filter_or(env_logger::DEFAULT_FILTER_ENV, "info"),
    )
    .init();

    let notify_need_reload_api_proxy = Arc::new(tokio::sync::Notify::new());
    let notify_received_config = notify_need_reload_api_proxy.clone();
    let notify_certs_rotated = notify_need_reload_api_proxy.clone();

    let client = config_monitor::get_sdk_client()?;
    let mut shutdown_sdk = client
        .inner()
        .shutdown_handle()
        .context("Could not create Shutdown handle")?;

    let report_twin_state_handle = client.report_twin_state_handle();

    let (twin_state_poll_handle, twin_state_poll_shutdown_handle) =
        config_monitor::report_twin_state(report_twin_state_handle);
    let (config_monitor_handle, config_monitor_shutdown_handle) =
        config_monitor::start(client, notify_received_config)
            .context("Failed running config monitor")?;
    let (cert_monitor_handle, cert_monitor_shutdown_handle) =
        certs_monitor::start(notify_certs_rotated)
            .context("Failed running certificates monitor")?;
    let (nginx_controller_handle, nginx_controller_shutdown_handle) =
        nginx_controller_start(notify_need_reload_api_proxy)
            .context("Failed running nginx controller")?;

    //If one task closes, clean up everything
    match nginx_controller_handle.await {
        Ok(_) => (),
        Err(err) => log::error!("Tasks encountered and error {:?}", err),
    };

    //Send shutdown signal to all task
    shutdown_sdk
        .shutdown()
        .await
        .context("Fatal, could not shut down SDK")?;

    futures::future::join_all(vec![
        cert_monitor_shutdown_handle.shutdown(),
        twin_state_poll_shutdown_handle.shutdown(),
        config_monitor_shutdown_handle.shutdown(),
        nginx_controller_shutdown_handle.shutdown(),
    ])
    .await;

    //Join all tasks.
    let all_task = vec![
        cert_monitor_handle,
        config_monitor_handle,
        twin_state_poll_handle,
    ];
    futures::future::join_all(all_task).await;

    log::info!("Gracefully exiting");
    Ok(())
}

pub fn nginx_controller_start(
    notify_need_reload_api_proxy: Arc<Notify>,
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
    let shutdown_handle = utils::ShutdownHandle(shutdown_signal.clone());

    //Wait for certificate rotation
    //This is just to avoid error at the beginning when nginx tries to start
    //but configuration is not ready

    let monitor_loop: JoinHandle<Result<()>> = tokio::spawn(async move {
        notify_need_reload_api_proxy.notified().await;

        loop {
            //Make sure proxy is stopped by sending stop command. Otherwise port will be blocked
            let command = Command::new(stop_proxy_program_path)
                .args(&stop_proxy_args)
                .spawn()
                .with_context(|| format!("Failed to start {:?} process.", stop_proxy_name))
                .context("Cannot stop proxy!")?;
            command
                .await
                .context("Error while trying to wait on stop proxy future")?;

            //Start nginx
            let child_nginx = Command::new(program_path)
                .args(&args)
                .stdout(Stdio::inherit())
                .spawn()
                .with_context(|| format!("Failed to start {:?} process.", name))
                .context("Cannot start proxy!")?;

            let signal_restart_nginx = notify_need_reload_api_proxy.notified();
            futures::pin_mut!(child_nginx, signal_restart_nginx);

            //Wait for: either a signal to restart(cert rotation, new config) or the child to crash.
            let restart_proxy = future::select(child_nginx, signal_restart_nginx);
            //Shutdown on ctrl+c or on signal

            let wait_shutdown_ctrl_c = signals::shutdown::shutdown();
            futures::pin_mut!(wait_shutdown_ctrl_c);
            let wait_shutdown_signal = shutdown_signal.notified();
            futures::pin_mut!(wait_shutdown_signal);

            let wait_shutdown = future::select(wait_shutdown_ctrl_c, wait_shutdown_signal);

            match futures::future::select(wait_shutdown, restart_proxy).await {
                Either::Left(_) => {
                    log::warn!("Shutting down ngxing controller!");
                    return Ok(());
                }
                Either::Right((result, _)) => {
                    match result {
                        Either::Left(_) => {
                            log::info!("Nginx crashed, restarting");
                        }
                        Either::Right(_) => {
                            log::info!("Request to restart Nginx received");
                        }
                    };
                }
            }

            log::info!("Restarting Proxy");
        }
    });

    Ok((monitor_loop, shutdown_handle))
}
//add pin utils
