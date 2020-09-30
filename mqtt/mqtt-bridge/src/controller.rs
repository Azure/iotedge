use std::collections::HashMap;

use futures_util::future::join_all;
use tracing::{error, info};

use crate::bridge::{Bridge, BridgeError};
use crate::settings::Settings;

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
#[derive(Default)]
pub struct BridgeController {
    bridges: HashMap<String, Bridge>,
}

impl BridgeController {
    pub fn new() -> Self {
        Self::default()
    }

    pub async fn start(
        &mut self,
        system_address: String,
        device_id: &str,
    ) -> Result<(), BridgeError> {
        info!("starting bridge controller...");
        let settings = Settings::new().map_err(BridgeError::LoadingSettings)?;

        if let Some(upstream) = settings.upstream() {
            let bridge = Bridge::new(system_address, device_id.into(), upstream.clone()).await?;
            self.bridges.insert(upstream.name().to_string(), bridge);
        } else {
            info!("No upstream settings detected. Not starting bridge.")
        };

        let mut bridge_futs = vec![];
        for (_, bridge) in self.bridges.iter_mut() {
            bridge_futs.push(bridge.start())
        }

        let bridges_status = join_all(bridge_futs).await;
        for status in bridges_status {
            if let Err(e) = status {
                // TODO PRE: give context to error
                error!(message = "error while running bridge", err = %e);
            }
        }

        Ok(())
    }
}
