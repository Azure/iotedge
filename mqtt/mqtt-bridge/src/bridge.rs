use crate::settings::NestedBridgeSettings;
use tracing::info;
use anyhow::{Result};

pub struct NestedBridge {
    nested_settings: NestedBridgeSettings,
}

impl NestedBridge {
    pub fn new(nested_settings: NestedBridgeSettings) -> Self {
        NestedBridge { nested_settings }
    }

    pub async fn start(&self) {
        info!(
            "Starting nested bridge...{:?}",
            self.nested_settings.gateway_hostname()
        );

        self.connect_to_local().await;
        self.connect_upstream().await;
    }

    async fn connect_upstream(&self) {
        info!("getting sas token from edgelet");
        let token = self.get_sas_token().await;
        // connect to upstream broker
    }

    async fn get_sas_token(&self) -> Result<String> {
        let uri = self.nested_settings.workload_uri();
    
        let client = edgelet_client::workload(uri.unwrap())?;
        
        let signature = String::from("ok");
        // client
        //     .sign(&module_id, &generation_id)
        //     .await?;
       

        Ok(signature)
    }

    async fn connect_to_local(&self)
    {

    }
}
