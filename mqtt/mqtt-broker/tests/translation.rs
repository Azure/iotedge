#![allow(dead_code)]
#![allow(unused_imports)]

use std::time::Duration;

use bytes::Bytes;
use futures_util::StreamExt;
use matches::assert_matches;
use mqtt3::{
    proto::{ClientId, Publication, QoS},
    Event, ReceivedPublication,
};

use common::TestClientBuilder;
use mqtt_broker::{AuthId, BrokerBuilder};

mod common;

#[tokio::test]
async fn basic_translation() {
    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut edge_hub_core = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithCleanSession("edge_hub_core".into()))
        .build();
    let mut device_1 = TestClientBuilder::new(address)
        .client_id(ClientId::IdWithCleanSession("device_1".into()))
        .build();

    // Core subscribes
    edge_hub_core
        .subscribe("$edgehub/+/twin/get/#", QoS::AtLeastOnce)
        .await;
    println!("Subscribed");

    // device requests twin update
    device_1
        .subscribe("$iothub/twin/res/10", QoS::AtLeastOnce)
        .await;
    device_1
        .publish_qos1("$iothub/twin/GET/10", "", false)
        .await;
    println!("Publish");

    // Core recieves request
    assert_matches!(
        edge_hub_core.publications().recv().await,
        Some(ReceivedPublication {
            topic_name,..
        }) if topic_name == String::from("$edgehub/device_1/twin/get/10")
    );
    edge_hub_core
        .publish_qos1("$edgehub/device_1/twin/res/10", "", false)
        .await;
    println!("recieve request");

    // device recieves response
    assert_matches!(
        device_1.publications().recv().await,
        Some(ReceivedPublication {
            topic_name,..
        }) if topic_name == String::from("$iothub/twin/res/10")
    );

    broker_shutdown.send(()).expect("can't stop the broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}
