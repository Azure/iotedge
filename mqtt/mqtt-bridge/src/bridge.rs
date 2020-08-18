use crate::settings::NestedBridgeSettings;
use tracing::info;

pub struct Bridge {
    nested_settings: NestedBridgeSettings,
}

impl Bridge {
    pub fn new(nested_settings: NestedBridgeSettings) -> Self {
        Bridge { nested_settings }
    }

    pub fn start(&self) {
        info!(
            "Starting nested bridge...{:?}",
            self.nested_settings.gateway_hostname()
        );
    }
}
