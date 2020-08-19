use crate::bridge::NestedBridge;
use crate::settings::Settings;
use tracing::info;

#[derive(Default)]
pub struct BridgeController {
    nested_bridge: Option<NestedBridge>,
}

impl BridgeController {
    pub fn new() -> Self {
        Self::default()
    }

    pub async fn start(&mut self) {
        // TODO: log if settings deserialize fails
        let settings = Settings::new().unwrap_or_default();
        match settings.nested_bridge().gateway_hostname() {
            Some(_) => {
                let nested_bridge = NestedBridge::new(settings.clone());
                nested_bridge.start().await;

                self.nested_bridge = Option::Some(nested_bridge);
            }
            None => info!("No nested bridge found."),
        };
    }
}
