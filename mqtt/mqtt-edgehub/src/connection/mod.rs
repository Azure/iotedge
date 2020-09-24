mod delivery;
mod translate;

use delivery::PublicationDelivery;
use translate::TranslateTopic;

use std::{collections::HashMap, sync::Arc};

use mqtt_broker::{BrokerHandle, ClientId, MakeMqttPacketProcessor, MakePacketProcessor};
use parking_lot::Mutex;

/// Creates a wrapper around default MQTT packet processor.
#[derive(Debug, Clone)]
pub struct MakeEdgeHubPacketProcessor<P> {
    broker_handle: BrokerHandle,
    inner: P,
}

impl MakeEdgeHubPacketProcessor<MakeMqttPacketProcessor> {
    pub fn new_default(broker_handle: BrokerHandle) -> Self {
        Self::new(broker_handle, MakeMqttPacketProcessor)
    }
}

impl<P> MakeEdgeHubPacketProcessor<P> {
    pub fn new(broker_handle: BrokerHandle, inner: P) -> Self {
        Self {
            broker_handle,
            inner,
        }
    }
}

impl<P> MakePacketProcessor for MakeEdgeHubPacketProcessor<P>
where
    P: MakePacketProcessor,
{
    type OutgoingProcessor = TranslateTopic<PublicationDelivery<P::OutgoingProcessor>>;

    type IncomingProcessor = TranslateTopic<PublicationDelivery<P::IncomingProcessor>>;

    fn make(&self, client_id: &ClientId) -> (Self::OutgoingProcessor, Self::IncomingProcessor) {
        let waited_to_be_acked = Arc::new(Mutex::new(HashMap::new()));

        let (outgoing_inner, incoming_inner) = self.inner.make(client_id);

        let inner = PublicationDelivery::new(
            self.broker_handle.clone(),
            outgoing_inner,
            waited_to_be_acked.clone(),
        );
        let outgoing = Self::OutgoingProcessor::new(client_id.clone(), inner);

        let inner = PublicationDelivery::new(
            self.broker_handle.clone(),
            incoming_inner,
            waited_to_be_acked,
        );
        let incoming = Self::IncomingProcessor::new(client_id.clone(), inner);

        (outgoing, incoming)
    }
}
