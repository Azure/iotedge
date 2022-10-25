// Example:
//
//     cargo run --example subscriber -- --server 127.0.0.1:1883 --client-id 'example-subscriber' --topic-filter foo --qos 1

use clap::Parser;
use futures_util::StreamExt;

mod common;

#[derive(Debug, Parser)]
struct Options {
    #[command(flatten)]
    common: common::Options,

    /// The topic filter to subscribe to.
    #[arg(long)]
    topic_filter: String,

    /// The QoS with which to subscribe to the topic.
    #[arg(long, value_parser = common::qos_from_str)]
    qos: mqtt3::proto::QoS,
}

#[tokio::main]
async fn main() {
    env_logger::Builder::from_env(env_logger::Env::new().filter_or(
        "MQTT3_LOG",
        "mqtt3=debug,mqtt3::logging=trace,subscriber=info",
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
        topic_filter,
        qos,
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

    let mut update_subscription_handle = client
        .update_subscription_handle()
        .expect("couldn't get subscription update handle");
    tokio::spawn(async move {
        let result = update_subscription_handle
            .subscribe(mqtt3::proto::SubscribeTo { topic_filter, qos })
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
