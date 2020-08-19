// An example Edge module client.
//
// - Connects to Azure IoT Edge Hub using bare TLS or WebSockets.
// - Responds to direct method requests by returning the same payload.
// - Reports twin state once at start, then updates it periodically after.
//
//
// Example:
//
//     cargo run --example edge_module -- --use-websocket --will 'azure-iot-mqtt client unexpectedly disconnected'
// Some `use` statements have been omitted here for brevity
use anyhow::Context;
use tokio::process::Command;
use std::process::Stdio;
use tokio::sync::Notify;
use std::{pin::Pin, sync::Arc};
mod monitors;
use futures_util::future::{self, Either};
use futures::Future;
use futures::executor::block_on;

#[tokio::main]
async fn main()  {
	env_logger::Builder::from_env(env_logger::Env::new().filter_or(env_logger::DEFAULT_FILTER_ENV, "info")).init();

	let notify_need_reload_api_proxy = Arc::new(Notify::new());
	let notify_received_config = notify_need_reload_api_proxy.clone();
	let notify_certs_rotated = notify_need_reload_api_proxy.clone();

	let shutdown_signal_twin_state = Arc::new(Notify::new());	
	let shutdown_signal_config_monitor = Arc::new(Notify::new());
	let shutdown_signal_cert_monitor = Arc::new(Notify::new());
	let shutdown_signal_loop_stask = Arc::new(Notify::new());

	let client = monitors::config_monitor::get_sdk_client();
	let mut shutdown_handle = client.inner().shutdown_handle().expect("couldn't get shutdown handle");


	let report_twin_state_handle = client.report_twin_state_handle();

	let twin_state_poll_task = monitors::config_monitor::poll_twin_state(report_twin_state_handle, shutdown_signal_twin_state.clone());
	let config_monitor_task = monitors::config_monitor::start(client , notify_received_config, shutdown_signal_config_monitor.clone());
	let cert_monitor_task = monitors::certs_monitor::start(notify_certs_rotated, shutdown_signal_cert_monitor.clone());
	let loop_task = nginx_controller_loop(notify_need_reload_api_proxy, shutdown_signal_loop_stask.clone());
	

	//Switch atomic var to true to tell all task to shutdown.
	ctrlc::set_handler(move ||  {
		block_on(shutdown_handle.shutdown()).expect("Could not shutdown gracefully");
		shutdown_signal_twin_state.notify();
		shutdown_signal_config_monitor.notify();
		shutdown_signal_cert_monitor.notify();
		shutdown_signal_loop_stask.notify();
    }).expect("Error setting Ctrl-C handler");


	//kill all client first
	futures::future::join_all(vec![
		Box::pin(twin_state_poll_task) as Pin<Box<dyn Future<Output = ()>>>,
		Box::pin(config_monitor_task) as Pin<Box<dyn Future<Output = ()>>>,
		Box::pin(cert_monitor_task) as Pin<Box<dyn Future<Output = ()>>>,
		Box::pin(loop_task) as Pin<Box<dyn Future<Output = ()>>>,
	]).await;

	log::warn!("Gracefully exiting");
}

pub async fn nginx_controller_loop(notify_need_reload_api_proxy: Arc<Notify>, shutdown_signal: Arc<Notify>){
	let program_path= "/usr/sbin/nginx";
	let args = vec!["-c".to_string(), "/app/nginx_config.conf".to_string(),"-g".to_string(),"daemon off;".to_string()];
	let name = "nginx";
	let stop_proxy_name = "stop nginx";
	let stop_proxy_program_path = "nginx";
	let stop_proxy_args = vec!["-s".to_string(), "stop".to_string()];


	//Wait for certificate rotation and for parse configuration.
	//This is just to avoid error at the beginning when nginx tries to start
	//but configuration is not ready
	notify_need_reload_api_proxy.notified().await;
	notify_need_reload_api_proxy.notified().await;

	loop{
		//Make sure proxy is stopped by sending stop command. Otherwise port will be blocked
		let command = Command::new(stop_proxy_program_path).args(&stop_proxy_args)
		.spawn()
		.with_context(|| format!("Failed to start {:?} process.", stop_proxy_name)).expect("Cannot stop proxy!");
		command.await.expect("Error while trying to wait on stop proxy future");


		//Start nginx
		let child_nginx = Command::new(program_path).args(&args)
		.stdout(Stdio::inherit())
		.spawn()
		.with_context(|| format!("Failed to start {:?} process.", name)).expect("Cannot start proxy!");

		let signal_restart_nginx =  notify_need_reload_api_proxy.notified();
		futures::pin_mut!(child_nginx,signal_restart_nginx);
		
		//Wait for: either a signal to restart(cert rotation, new config) or the child to crash.
		let restart_proxy = future::select(child_nginx, signal_restart_nginx);
		let wait_shutdown = shutdown_signal.notified();
		futures::pin_mut!(wait_shutdown);

		match future::select(wait_shutdown, restart_proxy).await{
			Either::Left(_) => {
				log::warn!("Shutting down ngxing controller!");
				return;
			},
			Either::Right((result, _)) => {
				match result {
					Either::Left(_) => {
						log::info!("Nginx crashed, restarting");						
					},
					Either::Right(_) => {
						log::info!("Request to restart Nginx received");
					}
				};
			},			
		}

		log::info!("Restarting Proxy");
	}
}
//add pin utils
