use anyhow::Result;
use tracing::info;

use crate::bridge::NestedBridge;
use crate::settings::Settings;

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
#[derive(Default)]
pub struct BridgeController {
    nested_bridge: Option<NestedBridge>,
}

impl BridgeController {
    pub fn new() -> Self {
        Self::default()
    }

    pub async fn start(&mut self) -> Result<()> {
        let settings = Settings::new()?;
        match settings.nested_bridge().gateway_hostname() {
            Some(_) => {
                let nested_bridge = NestedBridge::new(settings.clone());
                nested_bridge.start().await;

                self.nested_bridge = Option::Some(nested_bridge);
            }
            None => info!("No nested bridge found."),
        };

        Ok(())
    }
}
