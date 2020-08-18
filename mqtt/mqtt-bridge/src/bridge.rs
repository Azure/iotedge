use crate::settings::Settings;
use tracing::info;

pub struct BridgeController();

impl BridgeController {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn start(&self) {
        let settings = Settings::new().unwrap();
        settings.gateway_hostname().map(|gateway_hostname| {
            let nested_bridge = Bridge::new(gateway_hostname);
            nested_bridge.start();
        });
    }
}

impl Default for BridgeController {
    fn default() -> Self {
        Self {}
    }
}

pub struct Bridge {
    gateway_hostname: String,
}

impl Bridge {
    pub fn new(gateway_hostname: &str) -> Self {
        Bridge {
            gateway_hostname: gateway_hostname.to_string(),
        }
    }

    pub fn start(self) {
        info!("Starting nested bridge...{:?}", self.gateway_hostname);
    }
}
