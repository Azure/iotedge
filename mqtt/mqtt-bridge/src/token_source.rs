#![allow(dead_code)] // TODO remove when ready

use async_trait::async_trait;
use chrono::{DateTime, Utc};
use percent_encoding::{define_encode_set, percent_encode, PATH_SEGMENT_ENCODE_SET};
use url::form_urlencoded::Serializer as UrlSerializer;

use crate::settings::Credentials;

define_encode_set! {
    pub IOTHUB_ENCODE_SET = [PATH_SEGMENT_ENCODE_SET] | { '=' }
}

#[async_trait]
pub trait TokenSource {
    async fn get(&self, expiry: &DateTime<Utc>) -> Result<String, TokenSourceError>;
}

#[derive(Clone)]
pub struct SasTokenSource {
    creds: Credentials,
}

impl SasTokenSource {
    pub fn new(creds: Credentials) -> Self {
        SasTokenSource { creds }
    }
}

#[async_trait]
impl TokenSource for SasTokenSource {
    async fn get(&self, expiry: &DateTime<Utc>) -> Result<String, TokenSourceError> {
        let token: String = match &self.creds {
            Credentials::Provider(provider_settings) => {
                let expiry = expiry.timestamp().to_string();
                let audience = format!(
                    "{}/devices/{}/modules/{}",
                    provider_settings.iothub_hostname(),
                    provider_settings.device_id(),
                    provider_settings.module_id()
                );
                let resource_uri =
                    percent_encode(audience.to_lowercase().as_bytes(), IOTHUB_ENCODE_SET)
                        .to_string();
                let sig_data = format!("{}\n{}", &resource_uri, expiry);

                let client = edgelet_client::workload(provider_settings.workload_uri())
                    .map_err(TokenSourceError::CreateClient)?;
                let signature = client
                    .sign(
                        provider_settings.module_id(),
                        provider_settings.generation_id(),
                        &sig_data,
                    )
                    .await
                    .map_err(TokenSourceError::Sign)?;
                let signature = signature.digest();
                let token = UrlSerializer::new(format!("sr={}", resource_uri))
                    .append_pair("sig", &signature)
                    .append_pair("se", &expiry)
                    .finish();

                format!("SharedAccessSignature {}", token).to_owned()
            }
            Credentials::PlainText(creds) => creds.password().into(),
            Credentials::Anonymous(_) => "".into(),
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

    pub async fn get_trust_bundle(&self) -> Result<Option<String>, CertificateSourceError> {
        let certificate: Option<String> = match &self.creds {
            Credentials::Provider(provider_settings) => {
                let client = edgelet_client::workload(provider_settings.workload_uri())
                    .map_err(CertificateSourceError::CreateClient)?;
                let trust_bundle = client
                    .trust_bundle()
                    .await
                    .map_err(CertificateSourceError::TrustBundle)?;
                let cert = trust_bundle.certificate();
                Some(cert.to_owned())
            }
            _ => None,
        };

        Ok(certificate)
    }
}

#[derive(Debug, thiserror::Error)]
pub enum TokenSourceError {
    #[error("failed to save to store.")]
    Sign(#[from] edgelet_client::WorkloadError),

    #[error("failed to subscribe to topic.")]
    CreateClient(#[from] edgelet_client::Error),
}

#[derive(Debug, thiserror::Error)]
pub enum CertificateSourceError {
    #[error("failed to save to store.")]
    TrustBundle(#[from] edgelet_client::WorkloadError),

    #[error("failed to subscribe to topic.")]
    CreateClient(#[from] edgelet_client::Error),
}
