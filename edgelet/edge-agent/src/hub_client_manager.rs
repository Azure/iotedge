use std::{sync::Arc, time::Duration};

use futures_util::StreamExt;
use tokio::sync::Mutex;

use aziot_cert_client_async::Client as CertClient;
use aziot_identity_client_async::Client as IdentityClient;
use aziot_key_client_async::Client as KeyClient;
use azure_iot_mqtt::{
    module::{Client, Message},
    Authentication, Transport,
};

use crate::deployment::DeploymentManager;

type Result<T> = std::result::Result<T, Box<dyn std::error::Error + Send + Sync + 'static>>;

const EDGE_AGENT: &str = "$edgeAgent";

pub struct ClientManager {
    deployment_manager: Arc<Mutex<DeploymentManager>>,
    client: Client,
}

impl ClientManager {
    pub async fn new(
        iothub_hostname: String,
        device_id: &str,
        trust_bundle_path: &str,
        cert_client: Arc<CertClient>,
        key_client: Arc<KeyClient>,
        identity_client: Arc<IdentityClient>,
        deployment_manager: Arc<Mutex<DeploymentManager>>,
    ) -> Result<Self> {
        let client = make_client(
            iothub_hostname,
            device_id,
            trust_bundle_path,
            cert_client,
            key_client,
            identity_client,
        )
        .await?;

        Ok(Self {
            deployment_manager,
            client,
        })
    }

    pub fn start(mut self) {
        tokio::spawn(async move {
            while let Some(message) = self.client.next().await {
                match message {
                    Ok(Message::DirectMethod {
                        name,
                        payload,
                        request_id,
                    }) => log::debug!(
                        "Got direct method request: {}, {:?}, {}",
                        name,
                        payload,
                        request_id
                    ),
                    Ok(Message::ReportedTwinState(size)) => {
                        log::debug!("Got twin change ack: {:?}", size)
                    }
                    Ok(Message::TwinInitial(twin_initial)) => {
                        log::debug!("Got initial Twin: {:?}", twin_initial);
                        let mut deployment_manager = self.deployment_manager.lock().await;
                        if let Err(e) = deployment_manager.set_deployment(twin_initial).await {
                            log::error!("Set Deployment Error: {:#?}", e);
                        }
                    }
                    Ok(Message::TwinPatch(twin_patch)) => {
                        log::debug!("Got Twin Patch: {:?}", twin_patch);
                        let mut deployment_manager = self.deployment_manager.lock().await;
                        if let Err(e) = deployment_manager.update_deployment(twin_patch).await {
                            log::error!("Update Deployment Error: {:#?}", e);
                        }
                    }
                    Err(_) => todo!(),
                }
            }
        });
    }
}

async fn make_client(
    iothub_hostname: String,
    device_id: &str,
    trust_bundle_path: &str,
    cert_client: Arc<CertClient>,
    key_client: Arc<KeyClient>,
    identity_client: Arc<IdentityClient>,
) -> Result<Client> {
    // Make sas token
    let valid_period = Duration::from_secs(1000); // TODO: what should expiry time be?
    let (signature_data, make_sas_token) = azure_iot_mqtt::prepare_sas_token_request(
        &iothub_hostname,
        device_id,
        Some(EDGE_AGENT),
        valid_period,
    )?;

    let signature = sign(&signature_data, key_client, identity_client).await?;
    let token = make_sas_token(&signature);

    // get trust bundle
    let trust_bundle_cert = cert_client.get_cert(trust_bundle_path).await?;
    let authentication = Authentication::SasToken {
        token,
        server_root_certificate: vec![native_tls::Certificate::from_pem(&trust_bundle_cert)?],
    };

    let client = Client::new(
        iothub_hostname,
        device_id,
        EDGE_AGENT,
        authentication,
        Transport::Tcp,
        None,
        Duration::from_secs(300), //TODO
        Duration::from_secs(300), //TODO
    )
    .map_err(|e| format!("Make Hub Client Error: {:#?}", e))?;

    Ok(client)
}

async fn sign(
    signature_data: &str,
    key_client: Arc<KeyClient>,
    identity_client: Arc<IdentityClient>,
) -> Result<String> {
    // Get Key Handle from identity client
    let identity = identity_client.get_identity(EDGE_AGENT).await?;
    let key_handle = if let aziot_identity_common::Identity::Aziot(identity) = identity {
        identity
            .auth
            .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::Other, "missing auth field"))?
            .key_handle
            .ok_or_else(|| {
                std::io::Error::new(std::io::ErrorKind::Other, "missing key_handle field")
            })?
    } else {
        return Err(std::io::Error::new(
            std::io::ErrorKind::Other,
            "Got local identity for edgeAgent",
        )
        .into());
    };

    let signature = key_client
        .sign(
            &key_handle,
            aziot_key_common::SignMechanism::HmacSha256,
            signature_data.as_bytes(),
        )
        .await?;
    let signature = base64::encode(signature);

    Ok(signature)
}
