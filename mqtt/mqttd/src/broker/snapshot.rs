use mqtt_broker::{BrokerHandle, StateSnapshotHandle};

pub async fn snapshot(broker_handle: BrokerHandle, snapshot_handle: StateSnapshotHandle) {
    imp::snapshot(broker_handle, snapshot_handle).await;
}

#[cfg(unix)]
mod imp {
    use tokio::signal::unix::{signal, SignalKind};
    use tracing::{info, warn};

    use mqtt_broker::{BrokerHandle, Message, StateSnapshotHandle, SystemEvent};

    #[cfg(unix)]
    pub(super) async fn snapshot(
        mut broker_handle: BrokerHandle,
        snapshot_handle: StateSnapshotHandle,
    ) {
        let mut stream = match signal(SignalKind::user_defined1()) {
            Ok(stream) => stream,
            Err(e) => {
                warn!(message = "an error occurred setting up the signal handler", error=%e);
                return;
            }
        };

        info!("Setup to persist state on USR1 signal");
        loop {
            stream.recv().await;
            info!("Received signal USR1");
            if let Err(e) = broker_handle.send(Message::System(SystemEvent::StateSnapshot(
                snapshot_handle.clone(),
            ))) {
                warn!(message = "failed to signal the snapshotter", error=%e);
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
