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
use std::sync::Arc;
mod monitors;


fn main()  {
	env_logger::Builder::from_env(env_logger::Env::new().filter_or("AZURE_IOT_MQTT_LOG", "mqtt3=debug,mqtt3::logging=trace,azure_iot_mqtt=debug,edge_module=info")).init();

	
	let runtime = tokio::runtime::Runtime::new().expect("couldn't initialize tokio runtime");
	let runtime_handle = runtime.handle().clone();


	let notify_need_reload_api_proxy = Arc::new(Notify::new());
	let notify_received_config = notify_need_reload_api_proxy.clone();
	let notify_certs_rotated = notify_need_reload_api_proxy.clone();


	let runtime_health_monitor = runtime.handle().clone();
	runtime.handle().spawn_blocking(move ||monitors::api_proxy_health_monitor::start(runtime_health_monitor));	

	let runtime_config_monitor = runtime.handle().clone();
	runtime.handle().spawn_blocking(move || monitors::config_monitor::start(runtime_config_monitor, notify_received_config));

	let runtime_cert_monitor = runtime.handle().clone();
	runtime.handle().spawn_blocking(move ||monitors::certs_monitor::start(runtime_cert_monitor, notify_certs_rotated));

	let runtime_watchdog = runtime.handle().clone();
	runtime.handle().spawn_blocking(move ||watch_dog_loop(runtime_watchdog, notify_need_reload_api_proxy));

	//@Todo find a cleaner way to wait.
	loop{
		std::thread::sleep(std::time::Duration::new(1000, 0));
	}
}


fn watch_dog_loop(runtime_handle: tokio::runtime::Handle, notify_need_reload_api_proxy: Arc<Notify>){
	loop {
		let name = "reload nginx";
		let reload_proxy_program_path = "nginx";
		let reload_proxy_args = vec!["-s".to_string(), "reload".to_string()];
	
		//Block until we get a signal to reload nginx
		runtime_handle.block_on(notify_need_reload_api_proxy.notified());
		let child = Command::new(reload_proxy_program_path).args(&reload_proxy_args)
		.stdout(Stdio::inherit())
		.spawn()
		.with_context(|| format!("Failed to start {:?} process.", name)).expect("Cannot reload proxy!");

		runtime_handle.block_on(child).expect("Error while trying to wait on reload proxy future");
	}
}