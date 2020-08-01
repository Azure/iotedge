// Example:
//
//     cargo run --example subscriber -- --server 127.0.0.1:1883 --client-id 'example-subscriber' --topic-filter foo --qos 1

mod common;

#[derive(Debug, structopt::StructOpt)]
struct Options {
	#[structopt(help = "Address of the MQTT server.", long = "server")]
	server: std::net::SocketAddr,

	#[structopt(help = "Client ID used to identify this application to the server. If not given, a server-generated ID will be used.", long = "client-id")]
	client_id: Option<String>,

	#[structopt(help = "Username used to authenticate with the server, if any.", long = "username")]
	username: Option<String>,

	#[structopt(help = "Password used to authenticate with the server, if any.", long = "password")]
	password: Option<String>,

	#[structopt(
		help = "Maximum back-off time between reconnections to the server, in seconds.",
		long = "max-reconnect-back-off",
		default_value = "30",
		parse(try_from_str = common::duration_from_secs_str),
	)]
	max_reconnect_back_off: std::time::Duration,

	#[structopt(
		help = "Keep-alive time advertised to the server, in seconds.",
		long = "keep-alive",
		default_value = "5",
		parse(try_from_str = common::duration_from_secs_str),
	)]
	keep_alive: std::time::Duration,

	#[structopt(help = "The topic filter to subscribe to.", long = "topic-filter")]
	topic_filter: String,

	#[structopt(help = "The QoS with which to subscribe to the topic.", long = "qos", parse(try_from_str = common::qos_from_str))]
	qos: mqtt3::proto::QoS,
}

fn main() {
	env_logger::Builder::from_env(env_logger::Env::new().filter_or("MQTT3_LOG", "mqtt3=debug,mqtt3::logging=trace,subscriber=info")).init();

	let Options {
		server,
		client_id,
		username,
		password,
		max_reconnect_back_off,
		keep_alive,
		topic_filter,
		qos,
	} = structopt::StructOpt::from_args();

	let mut runtime = tokio::runtime::Runtime::new().expect("couldn't initialize tokio runtime");

	let mut client =
		mqtt3::Client::new(
			client_id,
			username,
			None,
			move || {
				let password = password.clone();
				Box::pin(async move {
					let io = tokio::net::TcpStream::connect(&server).await;
					io.map(|io| (io, password))
				})
			},
			max_reconnect_back_off,
			keep_alive,
		);

	let mut shutdown_handle = client.shutdown_handle().expect("couldn't get shutdown handle");
	runtime.spawn(async move {
		let () = tokio::signal::ctrl_c().await.expect("couldn't get Ctrl-C notification");
		let result = shutdown_handle.shutdown().await;
		let () = result.expect("couldn't send shutdown notification");
	});

	let mut update_subscription_handle = client.update_subscription_handle().expect("couldn't get subscription update handle");
	runtime.spawn(async move {
		let result = update_subscription_handle.subscribe(mqtt3::proto::SubscribeTo { topic_filter, qos }).await;
		if let Err(err) = result {
			panic!("couldn't update subscription: {}", err);
		}
	});

	let () = runtime.block_on(async {
		use futures_util::StreamExt;

		while let Some(event) = client.next().await {
			let event = event.unwrap();

			if let mqtt3::Event::Publication(publication) = event {
				match std::str::from_utf8(&publication.payload) {
					Ok(s) =>
						log::info!(
							"Received publication: {:?} {:?} {:?}",
							publication.topic_name,
							s,
							publication.qos,
						),
					Err(_) =>
						log::info!(
							"Received publication: {:?} {:?} {:?}",
							publication.topic_name,
							publication.payload,
							publication.qos,
						),
				}
			}
		}
	});
}
