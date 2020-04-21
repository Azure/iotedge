use futures_util::{
    future::{self, Either},
    StreamExt,
};
use mqtt_broker::{AuthId, BrokerBuilder, Server};
use std::future::Future;

#[tokio::test]
async fn it_sends_data() {
    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    // let (tx, rx) = tokio::sync::oneshot::<()>::channel();

    let shutdown = futures_util::future::pending();

    let transports = vec![mqtt_broker::TransportBuilder::Tcp("localhost:0")];
    let server = Server::from_broker(broker);
    let broker_handle = tokio::spawn(server.serve(transports, shutdown));

    let client_id = "foo".to_string();
    let username = None;
    let password = None;
    let server = "localhost:1883";

    let mut client = mqtt3::Client::new(
        Some(client_id.clone()),
        username.clone(),
        None,
        move || {
            let password = password.clone();
            Box::pin(async move {
                let io = tokio::net::TcpStream::connect(&server).await;
                io.map(|io| (io, password))
            })
        },
        std::time::Duration::from_secs(30),
        std::time::Duration::from_secs(5),
    );

    matches::assert_matches!(client.next().await, Some(Ok(mqtt3::Event::NewConnection {reset_session})) if reset_session );

    client.shutdown_handle().unwrap().shutdown().await.unwrap();

    let password = None;
    let mut client = mqtt3::Client::new(
        Some(client_id),
        username,
        None,
        move || {
            let password = password.clone();
            Box::pin(async move {
                let io = tokio::net::TcpStream::connect(&server).await;
                io.map(|io| (io, password))
            })
        },
        std::time::Duration::from_secs(30),
        std::time::Duration::from_secs(5),
    );

    matches::assert_matches!(client.next().await, Some(Ok(mqtt3::Event::NewConnection {reset_session})) if !reset_session );

    broker_handle.await;
}
