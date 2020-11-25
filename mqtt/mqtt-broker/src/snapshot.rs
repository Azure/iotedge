use std::collections::{HashMap, VecDeque};

use chrono::{DateTime, Utc};
use tokio::sync::mpsc::{self, Receiver, Sender};
use tracing::{info, warn};

use mqtt3::proto::{self, Publication};

use crate::{persist::Persist, ClientInfo, Error, Subscription};

/// Used for persisting/loading broker state.
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

/// Used for persisting/loading session state.
#[derive(Clone, Debug, PartialEq)]
pub struct SessionSnapshot {
    client_info: ClientInfo,
    subscriptions: HashMap<String, Subscription>,
    waiting_to_be_sent: VecDeque<Publication>,
    last_active: DateTime<Utc>,
}

impl SessionSnapshot {
    pub fn from_parts(
        client_info: ClientInfo,
        subscriptions: HashMap<String, Subscription>,
        waiting_to_be_sent: VecDeque<Publication>,
        last_active: DateTime<Utc>,
    ) -> Self {
        Self {
            client_info,
            subscriptions,
            waiting_to_be_sent,
            last_active,
        }
    }

    pub fn into_parts(
        self,
    ) -> (
        ClientInfo,
        HashMap<String, Subscription>,
        VecDeque<proto::Publication>,
        DateTime<Utc>,
    ) {
        (
            self.client_info,
            self.subscriptions,
            self.waiting_to_be_sent,
            self.last_active,
        )
    }
}

#[derive(Debug)]
enum Event {
    State(BrokerSnapshot),
    Shutdown,
}

#[derive(Debug, Clone)]
pub struct StateSnapshotHandle(Sender<Event>);

impl StateSnapshotHandle {
    pub fn try_send(&mut self, state: BrokerSnapshot) -> Result<(), Error> {
        self.0
            .try_send(Event::State(state))
            .map_err(|e| Error::SendSnapshotMessage(e.into()))?;
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
            .map_err(|e| Error::SendSnapshotMessage(e.into()))?;
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
                        warn!(message = "an error occurred persisting state snapshot.", error = %e);
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
