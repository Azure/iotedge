use tokio::sync::mpsc::{self, Receiver, Sender};
use tracing::{info, warn};

use crate::persist::Persist;
use crate::{BrokerState, Error};

enum Event {
    State(BrokerState),
    Shutdown,
}

#[derive(Clone, Debug)]
pub struct StateSnapshotHandle(Sender<Event>);

impl StateSnapshotHandle {
    pub async fn send(&mut self, state: BrokerState) -> Result<(), Error> {
        self.0
            .send(Event::State(state))
            .await
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
        let (sender, events) = mpsc::channel(1024);
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
