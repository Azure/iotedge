use std::collections::HashMap;

use tracing::info;

use crate::bridge::{Bridge, BridgeError, BridgeShutdownHandle};
use crate::settings::Settings;

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
#[derive(Default)]
pub struct BridgeController {
    bridges: HashMap<String, (Bridge, BridgeShutdownHandle)>,
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
        info!("starting bridge");
        let settings = Settings::new().map_err(BridgeError::LoadingSettings)?;

        if let Some(upstream) = settings.upstream() {
            let mut bridge = Bridge::new(system_address, device_id.into(), upstream.clone())?;

            let bridge_shutdown = bridge.start().await?;

            self.bridges
                .insert(upstream.name().to_string(), (bridge, bridge_shutdown));
        } else {
            info!("No upstream settings detected. Not starting bridge.")
        };
        Ok(())
    }
}
