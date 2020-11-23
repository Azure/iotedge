use tokio::{
    task::JoinHandle,
    time::{Duration, Instant},
};
use tracing::{info, warn};

use mqtt_broker::{BrokerHandle, Message, SystemEvent};

struct Janitor {}

pub async fn start_cleanup() {
    info!("starting cleanup job...");
}

async fn tick_cleanup(period: Duration, expiration: Duration, broker_handle: BrokerHandle) {
    info!("cleaning up expired sessions every {:?}", period);
    let start = Instant::now() + period;
    let mut interval = tokio::time::interval_at(start, period);
    loop {
        let instant = interval.tick().await;
        if let Err(e) = broker_handle.send(Message::System(SystemEvent::SessionCleanup(
            instant - expiration,
        ))) {
            warn!(message = "failed to tick the cleanup job", error = %e);
        }
    }
}
