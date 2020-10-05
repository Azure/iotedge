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

    pub async fn init(
        &mut self,
        system_address: String,
        device_id: &str,
    ) -> Result<(), BridgeError> {
        info!("initializing bridge...");
        let settings = Settings::new().map_err(BridgeError::LoadingSettings)?;

        if let Some(upstream) = settings.upstream() {
            let bridge = Bridge::new(system_address, device_id.into(), upstream.clone());
            self.bridges.insert(upstream.name().to_string(), bridge);
        } else {
            info!("No upstream settings detected. Not starting bridge.")
        };
        Ok(())
    }

    pub async fn run(self) {
        info!("starting bridge...");

        let mut bridge_handles = vec![];
        for (_, bridge) in self.bridges {
            let bridge_handle = tokio::spawn(async move { bridge.start().await });
            bridge_handles.push(bridge_handle);
        }

        let bridges_status = join_all(bridge_handles).await;
        for status in bridges_status {
            if let Err(e) = status {
                error!(message = "error while running bridge", err = %e);
            }
        }
    }
}
