use anyhow::Context;
use tokio::process::Command;
use std::process::Stdio;

pub fn start(runtime_handle: tokio::runtime::Handle){
	let program_path= "/usr/sbin/nginx";
	let args = vec!["-c".to_string(), "/home/iotedge-mqtt-rs/to_move/nginxconf/nginx_config.conf".to_string(),"-g".to_string(),"daemon off;".to_string()];
	let name = "nginx";
	let stop_proxy_name = "stop nginx";
	let stop_proxy_program_path = "nginx";
	let stop_proxy_args = vec!["-s".to_string(), "stop".to_string()];

	loop{
		//Make sure proxy is stopped by sending stop command. Otherwise port will be blocked
		let command = Command::new(stop_proxy_program_path).args(&stop_proxy_args)
		.spawn()
		.with_context(|| format!("Failed to start {:?} process.", stop_proxy_name)).expect("Cannot stop proxy!");
		runtime_handle.block_on(command).expect("Error while trying to wait on stop proxy future");


		let child = Command::new(program_path).args(&args)
		.stdout(Stdio::inherit())
		.spawn()
		.with_context(|| format!("Failed to start {:?} process.", name)).expect("Cannot start proxy!");

		runtime_handle.block_on(child).expect("Error while trying to wait on proxy future");
	}
}