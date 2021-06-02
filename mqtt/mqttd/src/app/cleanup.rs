use std::time::Duration as StdDuration;

use anyhow::Result;
use chrono::{Duration, Utc};
use tokio::time::{self, Instant};
use tracing::{info, warn};

use mqtt_broker::{BrokerHandle, Message, SystemEvent};

pub async fn start_cleanup(
    broker_handle: BrokerHandle,
    cleanup_interval: StdDuration,
    expiration: StdDuration,
) -> Result<()> {
    info!("starting cleanup job...");

    let expiration = Duration::from_std(expiration)?;
    let tick = tick_cleanup(cleanup_interval, expiration, broker_handle);
    tokio::spawn(tick);

    Ok(())
}

async fn tick_cleanup(period: StdDuration, expiration: Duration, broker_handle: BrokerHandle) {
    info!("cleaning up expired sessions every {:?}", period);
    let start = Instant::now() + period;
    let mut interval = time::interval_at(start, period);
    loop {
        interval.tick().await;
        if let Err(e) = broker_handle.send(Message::System(SystemEvent::SessionCleanup(
            Utc::now() - expiration,
        ))) {
            warn!(message = "failed to tick the cleanup job", error = %e);
        }
    }
}
