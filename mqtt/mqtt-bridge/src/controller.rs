use std::{collections::HashMap, env};

use tracing::info;

use crate::bridge::{Bridge, BridgeError};
use crate::settings::Settings;

const UPSTREAM_PROTOCOL: &str = "UpstreamProtocol";
const EXPECTED_UPSTREAM_PROTOCOL: &str = "mqtt";

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
#[derive(Default)]
pub struct BridgeController {
    bridges: HashMap<String, Bridge>,
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
            let upstream_protocol = env::var(UPSTREAM_PROTOCOL).ok();

            if let Some(protocol) = upstream_protocol {
                if protocol.to_lowercase() == EXPECTED_UPSTREAM_PROTOCOL {
                    let nested_bridge =
                        Bridge::new(system_address, device_id.into(), upstream.clone());

                    nested_bridge.start().await?;

                    self.bridges
                        .insert(upstream.name().to_string(), nested_bridge);
                } else {
                    info!("Upstream protocol is not MQTT. Not starting usptream bridge.")
                }
            }
        } else {
            info!("No upstream settings detected. Not starting bridge.")
        };
        Ok(())
    }
}
