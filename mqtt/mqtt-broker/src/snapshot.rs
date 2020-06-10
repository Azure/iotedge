use std::collections::{HashMap, HashSet, VecDeque};

use tokio::sync::mpsc::{self, Receiver, Sender};
use tracing::{info, warn};

use mqtt3::proto::{self, PacketIdentifier, Publication};
use mqtt_broker_core::ClientId;

use crate::persist::Persist;
use crate::{session::PacketIdentifiers, Error, Publish, Subscription};

#[derive(Clone, Default, Debug, PartialEq)]
pub struct BrokerSnapshot {
    retained: HashMap<String, Publication>,
    sessions: Vec<SessionSnapshot>,
}

impl BrokerSnapshot {
    pub fn new(retained: HashMap<String, Publication>, sessions: Vec<SessionSnapshot>) -> Self {
        Self { retained, sessions }
    }

    pub fn into_parts(self) -> (HashMap<String, Publication>, Vec<SessionSnapshot>) {
        (self.retained, self.sessions)
    }
}

#[derive(Clone, Debug, PartialEq)]
pub struct SessionSnapshot {
    client_id: ClientId,
    subscriptions: HashMap<String, Subscription>,
    packet_identifiers: PacketIdentifiers,
    waiting_to_be_sent: VecDeque<Publication>,
    waiting_to_be_acked: HashMap<PacketIdentifier, Publish>,
    waiting_to_be_released: HashMap<PacketIdentifier, proto::Publish>,
    waiting_to_be_completed: HashSet<PacketIdentifier>,
}

impl SessionSnapshot {
    pub fn from_parts(
        client_id: ClientId,
        subscriptions: HashMap<String, Subscription>,
        packet_identifiers: PacketIdentifiers,
        waiting_to_be_sent: VecDeque<Publication>,
        waiting_to_be_acked: HashMap<PacketIdentifier, Publish>,
        waiting_to_be_released: HashMap<PacketIdentifier, proto::Publish>,
        waiting_to_be_completed: HashSet<PacketIdentifier>,
    ) -> Self {
        Self {
            client_id,
            subscriptions,
            packet_identifiers,
            waiting_to_be_sent,
            waiting_to_be_acked,
            waiting_to_be_released,
            waiting_to_be_completed,
        }
    }

    pub fn into_parts(
        self,
    ) -> (
        ClientId,
        HashMap<String, Subscription>,
        PacketIdentifiers,
        VecDeque<proto::Publication>,
        HashMap<PacketIdentifier, Publish>,
        HashMap<PacketIdentifier, proto::Publish>,
        HashSet<PacketIdentifier>,
    ) {
        (
            self.client_id,
            self.subscriptions,
            self.packet_identifiers,
            self.waiting_to_be_sent,
            self.waiting_to_be_acked,
            self.waiting_to_be_released,
            self.waiting_to_be_completed,
        )
    }
}

enum Event {
    State(BrokerSnapshot),
    Shutdown,
}

#[derive(Clone, Debug)]
pub struct StateSnapshotHandle(Sender<Event>);

impl StateSnapshotHandle {
    pub fn try_send(&mut self, state: BrokerSnapshot) -> Result<(), Error> {
        self.0
            .try_send(Event::State(state))
            .map_err(|_| Error::SendSnapshotMessage)?;
        Ok(())
    }
}

#[derive(Debug)]
pub struct ShutdownHandle(Sender<Event>);

impl ShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), Error> {
        self.0
            .send(Event::Shutdown)
            .await
            .map_err(|_| Error::SendSnapshotMessage)?;
        Ok(())
    }
}

pub struct Snapshotter<P> {
    persistor: P,
    sender: Sender<Event>,
    events: Receiver<Event>,
}

impl<P> Snapshotter<P> {
    pub fn new(persistor: P) -> Self {
        let (sender, events) = mpsc::channel(5);
        Snapshotter {
            persistor,
            sender,
            events,
        }
    }

    pub fn snapshot_handle(&self) -> StateSnapshotHandle {
        StateSnapshotHandle(self.sender.clone())
    }

    pub fn shutdown_handle(&self) -> ShutdownHandle {
        ShutdownHandle(self.sender.clone())
    }
}

impl<P> Snapshotter<P>
where
    P: Persist,
{
    pub async fn run(mut self) -> P {
        while let Some(event) = self.events.recv().await {
            match event {
                Event::State(state) => {
                    if let Err(e) = self.persistor.store(state).await {
                        warn!(message = "an error occurred persisting state snapshot.", error=%e);
                    }
                }
                Event::Shutdown => {
                    info!("state snapshotter shutting down...");
                    break;
                }
            }
        }
        self.persistor
    }
}
