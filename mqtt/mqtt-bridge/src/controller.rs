use std::collections::HashMap;

use anyhow::Result;
use tracing::info;

use crate::bridge::Bridge;
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

    pub async fn start(&mut self) -> Result<()> {
        info!("starting bridge");

        let settings = Settings::new()?;
        if let Some(upstream) = settings.upstream() {
            let nested_bridge = Bridge::new(upstream.clone());
            nested_bridge.start().await;

            self.bridges
                .insert(upstream.name().to_string(), nested_bridge);
        } else {
            info!("No nested bridge found.")
        };
        Ok(())
    }
}
