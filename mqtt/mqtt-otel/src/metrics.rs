use lazy_static::lazy_static;
use opentelemetry::metrics::Counter;
use opentelemetry::{global, KeyValue};

struct BrokerOtelInstruments {
    pub client_msgs_received_counter: Counter<u64>,
    pub client_msgs_sent_counter: Counter<u64>,
}

impl BrokerOtelInstruments {
    fn new() -> BrokerOtelInstruments {
        let meter = global::meter("azure/iotedge/mqttbroker");
        BrokerOtelInstruments {
            client_msgs_received_counter: meter
                .u64_counter("mqtt.broker.client.messages.received")
                .with_description(
                    "Total number of client messages received by this MQTT Broker instance.",
                )
                .init(),
            client_msgs_sent_counter: meter
                .u64_counter("mqtt.broker.client.messages.sent")
                .with_description(
                    "Total number of client messages sent by this MQTT Broker instance.",
                )
                .init(),
        }
    }
}

lazy_static! {
    static ref BROKER_OTEL_INSTRUMENTS: BrokerOtelInstruments = { BrokerOtelInstruments::new() };
}

pub fn inc_client_msgs_received(client_id: String) {
    BROKER_OTEL_INSTRUMENTS
        .client_msgs_received_counter
        .add(1, &[KeyValue::new("client_id", client_id)]);
}

pub fn inc_client_msgs_sent(client_id: String) {
    BROKER_OTEL_INSTRUMENTS
        .client_msgs_sent_counter
        .add(1, &[KeyValue::new("client_id", client_id)]);
}
