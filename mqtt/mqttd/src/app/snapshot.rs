use tokio::{
    task::JoinHandle,
    time::{self, Duration, Instant},
};
use tracing::{info, warn};

use mqtt_broker::{
    BrokerHandle, FilePersistor, Message, ShutdownHandle, Snapshotter, StateSnapshotHandle,
    SystemEvent, VersionedFileFormat,
};

pub async fn start_snapshotter(
    broker_handle: BrokerHandle,
    persistor: FilePersistor<VersionedFileFormat>,
    snapshot_interval: Duration,
) -> (
    ShutdownHandle,
    JoinHandle<FilePersistor<VersionedFileFormat>>,
) {
    info!("starting snapshotter...");

    let snapshotter = Snapshotter::new(persistor);
    let snapshot_handle = snapshotter.snapshot_handle();
    let shutdown_handle = snapshotter.shutdown_handle();
    let join_handle = tokio::spawn(snapshotter.run());

    // Tick the snapshotter
    let tick = tick_snapshot(
        snapshot_interval,
        broker_handle.clone(),
        snapshot_handle.clone(),
    );
    tokio::spawn(tick);

    // Signal the snapshotter
    let snapshot = imp::snapshot(broker_handle, snapshot_handle);
    tokio::spawn(snapshot);

    (shutdown_handle, join_handle)
}

async fn tick_snapshot(
    period: Duration,
    broker_handle: BrokerHandle,
    snapshot_handle: StateSnapshotHandle,
) {
    info!("persisting state every {:?}", period);
    let start = Instant::now() + period;
    let mut interval = time::interval_at(start, period);
    loop {
        interval.tick().await;
        if let Err(e) = broker_handle.send(Message::System(SystemEvent::StateSnapshot(
            snapshot_handle.clone(),
        ))) {
            warn!(message = "failed to tick the snapshotter", error = %e);
        }
    }
}

#[cfg(unix)]
mod imp {
    use tokio::signal::unix::{signal, SignalKind};
    use tracing::{info, warn};

    use mqtt_broker::{BrokerHandle, Message, StateSnapshotHandle, SystemEvent};

    #[cfg(unix)]
    pub(super) async fn snapshot(
        broker_handle: BrokerHandle,
        snapshot_handle: StateSnapshotHandle,
    ) {
        let mut stream = match signal(SignalKind::user_defined1()) {
            Ok(stream) => stream,
            Err(e) => {
                warn!(message = "an error occurred setting up the signal handler", error = %e);
                return;
            }
        };

        info!("setup to persist state on USR1 signal");
        loop {
            stream.recv().await;
            info!("received signal USR1");
            if let Err(e) = broker_handle.send(Message::System(SystemEvent::StateSnapshot(
                snapshot_handle.clone(),
            ))) {
                warn!(message = "failed to signal the snapshotter", error = %e);
            }
        }
    }
}

#[cfg(not(unix))]
mod imp {
    use mqtt_broker::{BrokerHandle, StateSnapshotHandle};

    pub(super) async fn snapshot(
        _broker_handle: BrokerHandle,
        _snapshot_handle: StateSnapshotHandle,
    ) {
    }
}
