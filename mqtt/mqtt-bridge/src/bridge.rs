use tracing::info;

use crate::settings::Settings;

/// Bridge implementation for nested scenario.
/// It is used when `IOTEDGE_GATEWAYHOSTNAME` env variable is set.
pub struct NestedBridge {
    settings: Settings,
}

impl NestedBridge {
    pub fn new(settings: Settings) -> Self {
        NestedBridge { settings }
    }

    pub async fn start(&self) {
        info!("Starting nested bridge...{:?}", self.settings);

        self.connect_to_local().await;
        self.connect_upstream().await;
    }

    async fn connect_upstream(&self) {
        info!("connecting to upstream broker");
    }

    async fn connect_to_local(&self) {
        info!("connecting to local broker");
    }
}
