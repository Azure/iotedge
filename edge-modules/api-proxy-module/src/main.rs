#![deny(rust_2018_idioms, warnings)]
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

use anyhow::Context;
use futures_util::future::{self, Either};
use monitors::certs_monitor;
use monitors::config_monitor;
use std::sync::Arc;
use tokio::sync::Notify;

#[tokio::main]
async fn main() {
    env_logger::Builder::from_env(
        env_logger::Env::new().filter_or(env_logger::DEFAULT_FILTER_ENV, "info"),
    )
    .init();

    log::info!("PID is {}", std::process::id());
    let notify_need_reload_api_proxy = Arc::new(tokio::sync::Notify::new());
    let notify_received_config = notify_need_reload_api_proxy.clone();
    let notify_certs_rotated = notify_need_reload_api_proxy.clone();

    let client = config_monitor::get_sdk_client();
    let mut shutdown_sdk = client
        .inner()
        .shutdown_handle()
        .expect("couldn't get shutdown handle");

    let report_twin_state_handle = client.report_twin_state_handle();

    let (twin_state_poll_handle, twin_state_poll_shutdown_handle) =
        config_monitor::report_twin_state(report_twin_state_handle);
    let (config_monitor_handle, config_monitor_shutdown_handle) =
        config_monitor::start(client, notify_received_config);
    let (cert_monitor_handle, cert_monitor_shutdown_handle) =
        certs_monitor::start(notify_certs_rotated);
    let loop_task = nginx_controller_loop(notify_need_reload_api_proxy);

    //Main task closes on ctrl+c.
    loop_task.await;

    //Send shutdown signal to all task
    shutdown_sdk
        .shutdown()
        .await
        .expect("Fatal, could not shut down SDK");
    futures::future::join_all(vec![
        cert_monitor_shutdown_handle.shutdown(),
        twin_state_poll_shutdown_handle.shutdown(),
        config_monitor_shutdown_handle.shutdown(),
    ])
    .await;

    //Join all task.
    futures::future::join_all(vec![
        cert_monitor_handle,
        config_monitor_handle,
        twin_state_poll_handle,
    ])
    .await;

    log::info!("Gracefully exiting");
}

pub async fn nginx_controller_loop(notify_need_reload_api_proxy: Arc<Notify>) {
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

    //Wait for certificate rotation
    //This is just to avoid error at the beginning when nginx tries to start
    //but configuration is not ready
    notify_need_reload_api_proxy.notified().await;

    loop {
        //Make sure proxy is stopped by sending stop command. Otherwise port will be blocked
        let command = tokio::process::Command::new(stop_proxy_program_path)
            .args(&stop_proxy_args)
            .spawn()
            .with_context(|| format!("Failed to start {:?} process.", stop_proxy_name))
            .expect("Cannot stop proxy!");
        command
            .await
            .expect("Error while trying to wait on stop proxy future");

        //Start nginx
        let child_nginx = tokio::process::Command::new(program_path)
            .args(&args)
            .stdout(std::process::Stdio::inherit())
            .spawn()
            .with_context(|| format!("Failed to start {:?} process.", name))
            .expect("Cannot start proxy!");

        let signal_restart_nginx = notify_need_reload_api_proxy.notified();
        futures::pin_mut!(child_nginx, signal_restart_nginx);

        //Wait for: either a signal to restart(cert rotation, new config) or the child to crash.
        let restart_proxy = future::select(child_nginx, signal_restart_nginx);
        let wait_shutdown = signals::shutdown::shutdown();
        futures::pin_mut!(wait_shutdown);

        match futures::future::select(wait_shutdown, restart_proxy).await {
            Either::Left(_) => {
                log::warn!("Shutting down ngxing controller!");
                return;
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
}
//add pin utils
