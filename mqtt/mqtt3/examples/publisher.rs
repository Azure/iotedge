// Example:
//
//     cargo run --example publisher -- --server 127.0.0.1:1883 --client-id 'example-publisher' --publish-frequency 1000 --topic foo --qos 1 --payload 'hello, world'

use clap::Parser;
use futures_util::StreamExt;

mod common;

#[derive(Debug, Parser)]
struct Options {
    #[command(flatten)]
    common: common::Options,

    /// How often to publish to the server, in milliseconds.
    #[arg(
		long,
		default_value = "1000",
		value_parser = duration_from_millis_str,
	)]
    publish_frequency: std::time::Duration,

    /// The topic of the publications.
    #[arg(long)]
    topic: String,

    /// The QoS of the publications.
    #[arg(long, value_parser = common::qos_from_str)]
    qos: mqtt3::proto::QoS,

    /// The payload of the publications.
    #[arg(long)]
    payload: String,
}

#[tokio::main]
async fn main() {
    env_logger::Builder::from_env(env_logger::Env::new().filter_or(
        "MQTT3_LOG",
        "mqtt3=debug,mqtt3::logging=trace,publisher=info",
    ))
    .init();

    let Options {
        common:
            common::Options {
                server,
                client_id,
                username,
                password,
                max_reconnect_back_off,
                keep_alive,
            },
        publish_frequency,
        topic,
        qos,
        payload,
    } = Options::parse();

    let mut client = mqtt3::Client::new(
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

    let mut shutdown_handle = client
        .shutdown_handle()
        .expect("couldn't get shutdown handle");
    tokio::spawn(async move {
        tokio::signal::ctrl_c()
            .await
            .expect("couldn't get Ctrl-C notification");
        let result = shutdown_handle.shutdown().await;
        result.expect("couldn't send shutdown notification");
    });

    let payload: bytes::Bytes = payload.into();

    let publish_handle = client
        .publish_handle()
        .expect("couldn't get publish handle");
    tokio::spawn(async move {
        let mut interval = tokio::time::interval(publish_frequency);
        loop {
            interval.tick().await;

            let topic = topic.clone();
            log::info!("Publishing to {} ...", topic);

            let mut publish_handle = publish_handle.clone();
            let payload = payload.clone();

            tokio::spawn(async move {
                let result = publish_handle
                    .publish(mqtt3::proto::Publication {
                        topic_name: topic.clone(),
                        qos,
                        retain: false,
                        payload,
                    })
                    .await;
                result.expect("couldn't publish");
                log::info!("Published to {}", topic);
                Ok::<_, ()>(())
            });
        }
    });

    while let Some(event) = client.next().await {
        let _ = event.unwrap();
    }
}

fn duration_from_millis_str(
    s: &str,
) -> Result<std::time::Duration, <u64 as std::str::FromStr>::Err> {
    Ok(std::time::Duration::from_millis(s.parse()?))
}
