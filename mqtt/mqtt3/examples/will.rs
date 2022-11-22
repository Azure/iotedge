// This example demonstrates the use of a will.
//
// The client will connect to the server with a will built from the --topic, --qos and --payload parameters. It will then subscribe to the same topic.
// If the client receives a Ctrl-C, it will exit without properly shutting down the client. Thus the client will not be able to send a DISCONNECT
// to the server, so the server will publish the will to all subscribers.
//
// To demonstrate the effect, run two or more instances of this example with different client IDs (and optionally, different QoS and payloads)
// but the same topic subscription, then kill one with Ctrl-C. The other instances should all receive the will.
//
// Example:
//
//     cargo run --example will -- --server 127.0.0.1:1883 --client-id 'example-will-1' --topic foo --qos 1 --payload '"goodbye, world"  - example-will-1'
//     cargo run --example will -- --server 127.0.0.1:1883 --client-id 'example-will-2' --topic foo --qos 1 --payload '"goodbye, world"  - example-will-2'

use clap::Parser;
use futures_util::StreamExt;

mod common;

#[derive(Debug, Parser)]
struct Options {
    #[command(flatten)]
    common: common::Options,

    /// The topic of the will.
    #[arg(long)]
    topic: String,

    /// The QoS of the will.
    #[arg(long, value_parser = common::qos_from_str)]
    qos: mqtt3::proto::QoS,

    /// The payload of the will.
    #[arg(long)]
    payload: String,
}

#[tokio::main]
async fn main() {
    env_logger::Builder::from_env(
        env_logger::Env::new().filter_or("MQTT3_LOG", "mqtt3=debug,mqtt3::logging=trace,will=info"),
    )
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
        topic,
        qos,
        payload,
    } = Options::parse();

    let will = mqtt3::proto::Publication {
        topic_name: topic.clone(),
        qos,
        retain: false,
        payload: payload.into(),
    };

    let mut client = mqtt3::Client::new(
        client_id,
        username,
        Some(will),
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

    let mut update_subscription_handle = client
        .update_subscription_handle()
        .expect("couldn't get subscription update handle");
    tokio::spawn(async move {
        let result = update_subscription_handle
            .subscribe(mqtt3::proto::SubscribeTo {
                topic_filter: topic,
                qos,
            })
            .await;
        if let Err(err) = result {
            panic!("couldn't update subscription: {}", err);
        }
    });

    while let Some(event) = client.next().await {
        let event = event.unwrap();

        if let mqtt3::Event::Publication(publication) = event {
            match std::str::from_utf8(&publication.payload) {
                Ok(s) => log::info!(
                    "Received publication: {:?} {:?} {:?}",
                    publication.topic_name,
                    s,
                    publication.qos,
                ),
                Err(_) => log::info!(
                    "Received publication: {:?} {:?} {:?}",
                    publication.topic_name,
                    publication.payload,
                    publication.qos,
                ),
            }
        }
    }
}
