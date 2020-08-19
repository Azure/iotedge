use crate::settings::Settings;
use anyhow::Result;
use tracing::info;

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
        info!("getting sas token from edgelet");
        let token = self.get_sas_token().await;
        // connect to upstream broker
    }

    async fn get_sas_token(&self) -> Result<String> {
        let uri = self.settings.nested_bridge().workload_uri();

        let client = edgelet_client::workload(uri.unwrap())?;

        let signature = String::from("ok");
        // client
        //     .sign(&module_id, &generation_id)
        //     .await?;

        Ok(signature)
    }

    async fn connect_to_local(&self) {}
}
