use std::collections::{HashMap, VecDeque};
use std::time::Duration;
use std::time::Instant;

use async_trait::async_trait;
use tokio::sync::mpsc::{self, Receiver, Sender};
use tokio::task::JoinHandle;
use tracing::{info, warn};

use mqtt3::proto::{self, Publication};

use crate::sidecar::Sidecar;
use crate::sidecar::SidecarShutdownHandle;
use crate::{
    broker::BrokerHandle, persist::Persist, ClientId, Error, Message, Subscription, SystemEvent,
};

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
    client_id: ClientId,
    subscriptions: HashMap<String, Subscription>,
    waiting_to_be_sent: VecDeque<Publication>,
}

impl SessionSnapshot {
    pub fn from_parts(
        client_id: ClientId,
        subscriptions: HashMap<String, Subscription>,
        waiting_to_be_sent: VecDeque<Publication>,
    ) -> Self {
        Self {
            client_id,
            subscriptions,
            waiting_to_be_sent,
        }
    }

    pub fn into_parts(
        self,
    ) -> (
        ClientId,
        HashMap<String, Subscription>,
        VecDeque<proto::Publication>,
    ) {
        (self.client_id, self.subscriptions, self.waiting_to_be_sent)
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

// TODO: where P is persist?
pub struct SnapshotterShutdownHandle<P> {
    shutdown_handle: ShutdownHandle,
    join_handle: JoinHandle<P>,
}

impl<P> SidecarShutdownHandle for SnapshotterShutdownHandle<P> {
    fn shutdown(&self) {
        //    self.shutdown_handle.shutdown()
        todo!()
    }

    fn wait_for_shutdown(&self) {
        todo!()
    }
}

pub struct Snapshotter<P> {
    broker_handle: BrokerHandle,
    persistor: P,
    sender: Sender<Event>,
    events: Receiver<Event>,
}

impl<P> Snapshotter<P> {
    pub fn new(persistor: P, broker_handle: BrokerHandle) -> Self {
        let (sender, events) = mpsc::channel(5);
        Snapshotter {
            broker_handle,
            persistor,
            sender,
            events,
        }
    }

    fn snapshot_handle(&self) -> StateSnapshotHandle {
        StateSnapshotHandle(self.sender.clone())
    }

    fn shutdown_handle(&self) -> ShutdownHandle {
        ShutdownHandle(self.sender.clone())
    }
}

#[async_trait]
impl<P> Sidecar for Snapshotter<P>
where
    P: Persist,
{
    type ShutdownHandle = SnapshotterShutdownHandle<P>;
    fn run(&self) -> SnapshotterShutdownHandle<P> {
        let snapshot_handle = self.snapshot_handle();
        let shutdown_handle = self.shutdown_handle();
        let join_handle = tokio::spawn(self.listen());

        // Tick the snapshotter
        let tick = tick_snapshot(
            Duration::from_secs(5 * 60),
            self.broker_handle.clone(),
            snapshot_handle.clone(),
        );
        tokio::spawn(tick);

        // Signal the snapshotter
        let snapshot = snapshot::snapshot(self.broker_handle, snapshot_handle);
        tokio::spawn(snapshot);

        // (shutdown_handle, join_handle)
        SnapshotterShutdownHandle {
            join_handle,
            shutdown_handle,
        }
    }
}

// async fn start_snapshotter(
//     broker_handle: BrokerHandle,
//     persistor: FilePersistor<VersionedFileFormat>,
// ) -> (
//     SnapshotShutdownHandle,
//     JoinHandle<FilePersistor<VersionedFileFormat>>,
// ) {
//     let snapshotter = Snapshotter::new(persistor);
//     let snapshot_handle = snapshotter.snapshot_handle();
//     let shutdown_handle = snapshotter.shutdown_handle();
//     let join_handle = tokio::spawn(snapshotter.run());

//     // Tick the snapshotter
//     let tick = tick_snapshot(
//         Duration::from_secs(5 * 60),
//         broker_handle.clone(),
//         snapshot_handle.clone(),
//     );
//     tokio::spawn(tick);

//     // Signal the snapshotter
//     let snapshot = snapshot::snapshot(broker_handle, snapshot_handle);
//     tokio::spawn(snapshot);

//     (shutdown_handle, join_handle)
// }

impl<P> Snapshotter<P>
where
    P: Persist,
{
    async fn listen(mut self) -> P {
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

async fn tick_snapshot(
    period: Duration,
    mut broker_handle: BrokerHandle,
    snapshot_handle: StateSnapshotHandle,
) {
    info!("Persisting state every {:?}", period);
    let start = Instant::now() + period;
    let mut interval = tokio::time::interval_at(start, period);
    loop {
        interval.tick().await;
        if let Err(e) = broker_handle
            .send(Message::System(SystemEvent::StateSnapshot(
                snapshot_handle.clone(),
            )))
            .await
        {
            warn!(message = "failed to tick the snapshotter", error=%e);
        }
    }
}
