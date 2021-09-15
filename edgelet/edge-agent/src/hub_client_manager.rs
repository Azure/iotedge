use std::{sync::Arc, time::Duration};

use futures_util::StreamExt;
use tokio::sync::Mutex;

use azure_iot_mqtt::{
    module::{Client, Message},
    Authentication, Transport,
};

use crate::deployment::DeploymentManager;

type Result<T> = std::result::Result<T, Box<dyn std::error::Error>>;

struct ClientParams {
    device_id: String,
    module_id: String,
    generation_id: String,
    edgehub_hostname: String,
    iothub_hostname: String,
    workload_url: url::Url,
}

struct ClientManager {
    deployment_manager: Arc<Mutex<DeploymentManager>>,
    client: Client,
}

impl ClientManager {
    async fn new(
        deployment_manager: Arc<Mutex<DeploymentManager>>,
        client_params: ClientParams,
    ) -> Result<Self> {
        let client = make_client(client_params).await?;

        Ok(Self {
            deployment_manager,
            client,
        })
    }

    fn start(mut self) {
        tokio::spawn(async move {
            while let Some(message) = self.client.next().await {
                match message {
                    Ok(Message::DirectMethod {
                        name,
                        payload,
                        request_id,
                    }) => todo!(),
                    Ok(Message::ReportedTwinState(size)) => todo!(),
                    Ok(Message::TwinInitial(twin_initial)) => todo!(),
                    Ok(Message::TwinPatch(twin_patch)) => todo!(),
                    Err(_) => todo!(),
                }
            }
        });
    }
}

async fn make_client(client_params: ClientParams) -> Result<Client> {
    let ClientParams {
        device_id,
        module_id,
        generation_id,
        edgehub_hostname,
        iothub_hostname,
        workload_url,
    } = client_params;
    let authentication = Authentication::IotEdge {
        device_id: device_id.clone(),
        module_id: module_id.clone(),
        generation_id,
        iothub_hostname,
        workload_url,
    };

    let client = Client::new(
        edgehub_hostname,
        &device_id,
        &module_id,
        authentication,
        Transport::WebSocket,
        None,
        Duration::from_secs(300), //TODO
        Duration::from_secs(300), //TODO
    )?;

    Ok(client)
}
