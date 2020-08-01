use super::utils;
use tokio::sync::Notify;
use std::sync::Arc;
use anyhow::{Context, Result};

const PROXY_CONFIG_TAG:&str = "proxy config"; 
const PROXY_CONFIG_PATH:&str = "foo.txt"; 

fn duration_from_secs_str(s: &str) -> Result<std::time::Duration, <u64 as std::str::FromStr>::Err> {
	Ok(std::time::Duration::from_secs(s.parse()?))
}


#[derive(Debug, structopt::StructOpt)]
struct Options {
	#[structopt(help = "Whether to use websockets or bare TLS to connect to the Iot Hub", long = "use-websocket")]
	use_websocket: bool,

	#[structopt(help = "Will message to publish if this client disconnects unexpectedly", long = "will")]
	will: Option<String>,

	#[structopt(
		help = "Maximum back-off time between reconnections to the server, in seconds.",
		long = "max-back-off",
		default_value = "30",
		parse(try_from_str = duration_from_secs_str),
	)]
	max_back_off: std::time::Duration,

	#[structopt(
		help = "Keep-alive time advertised to the server, in seconds.",
		long = "keep-alive",
		default_value = "5",
		parse(try_from_str = duration_from_secs_str),
	)]
	keep_alive: std::time::Duration,

	#[structopt(
		help = "Interval at which the client reports its twin state to the server, in seconds.",
		long = "report-twin-state-period",
		default_value = "5",
		parse(try_from_str = duration_from_secs_str),
	)]
	report_twin_state_period: std::time::Duration,
}

pub fn start(runtime_handle: tokio::runtime::Handle, notify_received_config: Arc<Notify>){
	use futures_util::StreamExt;
	
	let Options {
		use_websocket,
		will,
		max_back_off,
		keep_alive,
		report_twin_state_period,
	} = structopt::StructOpt::from_args();


	let mut client = azure_iot_mqtt::module::Client::new_for_edge_module(
		if use_websocket { azure_iot_mqtt::Transport::WebSocket } else { azure_iot_mqtt::Transport::Tcp },

		will.map(Into::into),

		max_back_off,
		keep_alive,
	).expect("could not create client");

	spawn_background_tasks(
		&runtime_handle,
		client.inner().shutdown_handle(),
		client.report_twin_state_handle(),
		report_twin_state_period,
	);

	while let Some(message) = runtime_handle.block_on(client.next()) {
		let message = message.unwrap();

		log::info!("received message {:?}", message);

		if let azure_iot_mqtt::module::Message::TwinPatch(twin) = message {
			if let Err(err) = save_config(&twin)
			{
				log::error!("received message {:?}", err);
			}else
			{
				notify_received_config.notify();
			}
		};
	}
}


fn save_config(twin: &azure_iot_mqtt::TwinProperties)  -> Result<()>
{
	let json = twin.properties.get_key_value("hello");

	//Get value associated with the key and extract is as a string.
	let str = (*(json.context(format!("Key {} not found in twin", PROXY_CONFIG_TAG))?.1)).as_str();
	
	let encoded_file =str.context("Cannot extract json as base64 string")?;

	let bytes = base64::decode(encoded_file).context("Cannot decode base64 string")?;

	utils::write_binary_to_file(&bytes,PROXY_CONFIG_PATH)?;

	Ok(())
}

fn spawn_background_tasks(
	runtime_handle: &tokio::runtime::Handle,
	shutdown_handle: Result<mqtt3::ShutdownHandle, mqtt3::ShutdownError>,
	mut report_twin_state_handle: azure_iot_mqtt::ReportTwinStateHandle,
	report_twin_state_period: std::time::Duration,
) {
	let mut shutdown_handle = shutdown_handle.expect("couldn't get shutdown handle");
	runtime_handle.spawn(async move {
		let () = tokio::signal::ctrl_c().await.expect("couldn't get Ctrl-C notification");
		let result = shutdown_handle.shutdown().await;
		let () = result.expect("couldn't send shutdown notification");
	});

	runtime_handle.spawn(async move {
		use futures_util::StreamExt;

		let result = report_twin_state_handle.report_twin_state(azure_iot_mqtt::ReportTwinStateRequest::Replace(
			vec![("start-time".to_string(), chrono::Utc::now().to_string().into())].into_iter().collect()
		)).await;
		let () = result.expect("couldn't report initial twin state");

		let mut interval = tokio::time::interval(report_twin_state_period);
		while interval.next().await.is_some() {
			let result = report_twin_state_handle.report_twin_state(azure_iot_mqtt::ReportTwinStateRequest::Patch(
				vec![("current-time".to_string(), chrono::Utc::now().to_string().into())].into_iter().collect()
			)).await;

			let () = result.expect("couldn't report twin state patch");
		}
	});
}