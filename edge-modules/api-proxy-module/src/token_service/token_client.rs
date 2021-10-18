use anyhow::{Error, Result};
use log::info;
use percent_encoding::percent_encode;
use url::form_urlencoded::Serializer as UrlSerializer;

use edgelet_client::IOTHUB_ENCODE_SET;

pub struct TokenClient {
    device_id: String,
    module_id: String,
    generation_id: String,
    iothub_hostname: String,
    work_load_api_client: edgelet_client::WorkloadClient,
}

impl TokenClient {
    pub fn new(
        device_id: String,
        module_id: String,
        generation_id: String,
        iothub_hostname: String,
        work_load_api_client: edgelet_client::WorkloadClient,
    ) -> Self {
        TokenClient {
            device_id,
            module_id,
            generation_id,
            iothub_hostname,
            work_load_api_client,
        }
    }

    pub async fn get_new_sas_token(&self, expiration_date: &str) -> Result<String, Error> {
        let audience = format!(
            "{}/devices/{}/modules/{}",
            self.iothub_hostname,
            percent_encode(self.device_id.as_bytes(), IOTHUB_ENCODE_SET).to_string(),
            percent_encode(self.module_id.as_bytes(), IOTHUB_ENCODE_SET).to_string()
        );

        let resource_uri =
            percent_encode(audience.to_lowercase().as_bytes(), IOTHUB_ENCODE_SET).to_string();

        let sig_data = format!("{}\n{}", &resource_uri, expiration_date);

        let resp = self
            .work_load_api_client
            .sign(&self.module_id, &self.generation_id, &sig_data)
            .await?;

        let signature = resp.digest();
        let token = UrlSerializer::new(format!("SharedAccessSignature sr={}", resource_uri))
            .append_pair("sig", signature)
            .append_pair("se", expiration_date)
            .finish();

        info!("Successfully generated new token");

        Ok(token)
    }
}
