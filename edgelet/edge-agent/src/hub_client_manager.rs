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

type Result<T> = std::result::Result<T, Box<dyn std::error::Error>>;

const EDGE_AGENT: &str = "$edgeAgent";

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

async fn make_client(
    iothub_hostname: String,
    device_id: &str,
    trust_bundle_path: &str,
    cert_client: Arc<CertClient>,
    key_client: Arc<KeyClient>,
    identity_client: Arc<IdentityClient>,
) -> Result<Client> {
    let token = get_sas_token(
        &iothub_hostname,
        device_id,
        trust_bundle_path,
        key_client,
        identity_client,
    )
    .await?;
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
    )?;

    Ok(client)
}

async fn get_sas_token(
    iothub_hostname: &str,
    device_id: &str,
    trust_bundle_path: &str,
    key_client: Arc<KeyClient>,
    identity_client: Arc<IdentityClient>,
) -> Result<String> {
    // Get Expiry for sas token
    let since_unix_epoch = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map_err(|err| {
            std::io::Error::new(
                std::io::ErrorKind::Other,
                format!("could not get current time: {}", err),
            )
        })?;
    let expiry = since_unix_epoch + Duration::from_secs(1000); // TODO: proper expiry
    let expiry = expiry.as_secs().to_string();

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

    // Compose resource uri
    let resource_uri = format!(
        "{}/devices/{}/modules/{}",
        iothub_hostname, device_id, EDGE_AGENT
    );
    let resource_uri: String =
        percent_encoding::utf8_percent_encode(&resource_uri, azure_iot_mqtt::IOTHUB_ENCODE_SET)
            .collect();

    // Sign token
    let signature_data = format!("{}\n{}", resource_uri, expiry);
    let signature = key_client
        .sign(
            &key_handle,
            aziot_key_common::SignMechanism::HmacSha256,
            signature_data.as_bytes(),
        )
        .await?;
    let signature = base64::encode(signature);

    //Create token
    let token = {
        let mut serializer = url::form_urlencoded::Serializer::new(format!(
            "SharedAccessSignature sr={}",
            resource_uri
        ));
        serializer.append_pair("se", &expiry);
        serializer.append_pair("sig", &signature);
        serializer.finish()
    };

    Ok(token)
}
