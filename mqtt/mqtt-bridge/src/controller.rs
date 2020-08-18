use crate::bridge::Bridge;
use crate::settings::Settings;
use std::collections::HashMap;
use tracing::info;

#[derive(Default)]
pub struct BridgeController {
    briges: HashMap<String, Bridge>,
}

impl BridgeController {
    pub fn new() -> Self {
        Self::default()
    }

    pub async fn start(&mut self) {
        let settings = Settings::new().unwrap_or_default();
        match settings.nested_bridge().gateway_hostname() {
            Some(_) => {
                let nested_bridge = Bridge::new(settings.nested_bridge().clone());
                nested_bridge.start();

                self.briges.insert("Upstream".to_string(), nested_bridge);
            }
            None => info!("No nested bridge found."),
        };
    }
}
