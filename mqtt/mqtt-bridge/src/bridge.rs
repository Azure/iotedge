use tracing::info;

use crate::settings::ConnectionSettings;

/// Bridge implementation that connects to local broker and remote broker and handles messages flow
pub struct Bridge {
    connection_settings: ConnectionSettings,
}

impl Bridge {
    pub fn new(connection_settings: ConnectionSettings) -> Self {
        Bridge {
            connection_settings,
        }
    }

    pub async fn start(&self) {
        info!("Starting nested bridge...{:?}", self.connection_settings);

        self.connect_to_local().await;
        self.connect_to_remote().await;
    }

    async fn connect_to_remote(&self) {
        info!("connecting to remote broker");
    }

    async fn connect_to_local(&self) {
        info!("connecting to local broker");
    }
}
