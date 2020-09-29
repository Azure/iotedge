#![allow(dead_code)] // TODO remove when ready

use std::io::{Error, ErrorKind};

use async_trait::async_trait;
use chrono::{DateTime, Utc};
use percent_encoding::{define_encode_set, percent_encode, PATH_SEGMENT_ENCODE_SET};
use url::form_urlencoded::Serializer as UrlSerializer;

use crate::settings::{CredentialProviderSettings, Credentials};

define_encode_set! {
    pub IOTHUB_ENCODE_SET = [PATH_SEGMENT_ENCODE_SET] | { '=' }
}

#[async_trait]
pub trait TokenSource {
    async fn get(&self, expiry: &DateTime<Utc>) -> Result<Option<String>, Error>;
}

#[derive(Clone)]
pub struct SasTokenSource {
    creds: Credentials,
}

impl SasTokenSource {
    pub fn new(creds: Credentials) -> Self {
        SasTokenSource { creds }
    }

    async fn generate_sas_token(
        &self,
        provider_settings: &CredentialProviderSettings,
        expiry: &DateTime<Utc>,
    ) -> Result<String, Error> {
        let expiry = expiry.timestamp().to_string();
        let audience = format!(
            "{}/devices/{}/modules/{}",
            provider_settings.iothub_hostname(),
            percent_encode(provider_settings.device_id().as_bytes(), IOTHUB_ENCODE_SET).to_string(),
            percent_encode(provider_settings.module_id().as_bytes(), IOTHUB_ENCODE_SET).to_string()
        );
        let resource_uri =
            percent_encode(audience.to_lowercase().as_bytes(), IOTHUB_ENCODE_SET).to_string();
        let sig_data = format!("{}\n{}", &resource_uri, expiry);

        let client = edgelet_client::workload(provider_settings.workload_uri()).map_err(|e| {
            Error::new(
                ErrorKind::Other,
                format!("could not create workload client: {}", e),
            )
        })?;
        let signature = client
            .sign(
                provider_settings.module_id(),
                provider_settings.generation_id(),
                &sig_data,
            )
            .await
            .map_err(|e| Error::new(ErrorKind::Other, format!("could not get signature: {}", e)))?;
        let signature = signature.digest();
        let token = UrlSerializer::new(format!("sr={}", resource_uri))
            .append_pair("sig", &signature)
            .append_pair("se", &expiry)
            .finish();

        Ok(token)
    }
}

#[async_trait]
impl TokenSource for SasTokenSource {
    async fn get(&self, expiry: &DateTime<Utc>) -> Result<Option<String>, Error> {
        let token = match &self.creds {
            Credentials::Provider(provider_settings) => {
                let token = self
                    .get_token_from_workload(provider_settings, expiry)
                    .await?;
                Some(format!("SharedAccessSignature {}", token))
            }
            Credentials::PlainText(creds) => Some(creds.password().into()),
            Credentials::Anonymous(_) => None,
        };

        Ok(token)
    }
}

#[derive(Clone)]
pub struct TrustBundleSource {
    creds: Credentials,
}

impl TrustBundleSource {
    pub fn new(creds: Credentials) -> Self {
        Self { creds }
    }

    pub async fn get_trust_bundle(&self) -> Result<Option<String>, Error> {
        let certificate: Option<String> = match &self.creds {
            Credentials::Provider(provider_settings) => {
                let client =
                    edgelet_client::workload(provider_settings.workload_uri()).map_err(|e| {
                        Error::new(
                            ErrorKind::Other,
                            format!("could not create workload client: {}", e),
                        )
                    })?;
                let trust_bundle = client.trust_bundle().await.map_err(|e| {
                    Error::new(
                        ErrorKind::Other,
                        format!("failed to get trusted certificate: {}", e),
                    )
                })?;
                let cert = trust_bundle.certificate();
                Some(cert.to_owned())
            }
            _ => None,
        };

        Ok(certificate)
    }
}
