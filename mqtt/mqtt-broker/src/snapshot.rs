use std::thread;

use crossbeam_channel::{Receiver, Sender};
use tracing::{error, info, warn};

use crate::persist::Persist;
use crate::{BrokerState, Error};

pub(crate) enum Event {
    State(BrokerState),
    Shutdown,
}

#[derive(Clone, Debug)]
pub struct StateSnapshotHandle(Sender<Event>);

impl StateSnapshotHandle {
    pub fn try_send(&mut self, state: BrokerState) -> Result<(), Error> {
        self.0
            .send(Event::State(state))
            .map_err(|_e| Error::SendSnapshotMessage)?;
        Ok(())
    }
}

#[derive(Debug)]
pub struct ShutdownHandle(Sender<Event>);

impl ShutdownHandle {
    pub fn try_shutdown(&mut self) -> Result<(), Error> {
        self.0
            .send(Event::Shutdown)
            .map_err(|_e| Error::SendSnapshotMessage)?;
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
        let (sender, events) = crossbeam_channel::bounded(5);
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
    P: Persist + Send + 'static,
{
    pub async fn run(self) -> P {
        let (tx, rx) = tokio::sync::oneshot::channel();
        thread::spawn(|| {
            let persist = self.snapshotter_loop();
            if let Err(_e) = tx.send(persist) {
                error!("failed to send persist to event loop on shutdown");
            }
        });

        // TODO: return errors from the snapshotter run
        let result = rx.await;
        result.unwrap()
    }

    fn snapshotter_loop(mut self) -> P {
        while let Ok(event) = self.events.recv() {
            match event {
                Event::State(state) => {
                    if let Err(e) = self.persistor.store(state) {
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
