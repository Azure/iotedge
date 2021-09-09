use core::{convert::TryInto, num::TryFromIntError};
use std::{
    io::{Error, ErrorKind},
    pin::Pin,
    result::Result,
    str,
};

use async_trait::async_trait;
use chrono::{DateTime, Utc};
use futures_util::future::{self, BoxFuture};
use openssl::{ssl::SslConnector, ssl::SslMethod, x509::X509};
use percent_encoding::percent_encode;
use serde::Deserialize;
use tokio::{io::AsyncRead, io::AsyncWrite, net::TcpStream};
use tokio_openssl::SslStream;
use tracing::error;
use url::form_urlencoded::Serializer as UrlSerializer;

use edgelet_client::IOTHUB_ENCODE_SET;
use mqtt3::IoSource;

const DEFAULT_TOKEN_DURATION_MINS: i64 = 60;

#[derive(Clone)]
pub enum ClientIoSource {
    Tcp(TcpConnection<SasTokenSource>),
    Tls(TcpConnection<SasTokenSource>),
}

pub trait ClientIo: AsyncRead + AsyncWrite + Send + Sync + 'static {}

impl<I> ClientIo for I where I: AsyncRead + AsyncWrite + Send + Sync + 'static {}

type ClientIoSourceFuture =
    BoxFuture<'static, Result<(Pin<Box<dyn ClientIo>>, Option<String>), Error>>;

#[derive(Clone)]
pub struct TcpConnection<T>
where
    T: TokenSource + Clone + Send + Sync + 'static,
{
    address: String,
    token_source: Option<T>,
    trust_bundle_source: Option<TrustBundleSource>,
}

impl<T> TcpConnection<T>
where
    T: TokenSource + Clone + Send + Sync + 'static,
{
    pub fn new(
        address: impl Into<String>,
        token_source: Option<T>,
        trust_bundle_source: Option<TrustBundleSource>,
    ) -> Self {
        Self {
            address: address.into(),
            token_source,
            trust_bundle_source,
        }
    }
}

impl IoSource for ClientIoSource {
    type Io = Pin<Box<dyn ClientIo>>;

    type Error = Error;

    #[allow(clippy::type_complexity)]
    type Future = BoxFuture<'static, Result<(Self::Io, Option<String>), Self::Error>>;

    fn connect(&mut self) -> Self::Future {
        match self {
            ClientIoSource::Tcp(connect_settings) => Self::get_tcp_source(connect_settings.clone()),
            ClientIoSource::Tls(connect_settings) => Self::get_tls_source(connect_settings.clone()),
        }
    }
}

impl ClientIoSource {
    fn get_tcp_source(connection_settings: TcpConnection<SasTokenSource>) -> ClientIoSourceFuture {
        let address = connection_settings.address;
        let token_source = connection_settings.token_source;

        Box::pin(async move {
            let expiry = Utc::now() + chrono::Duration::minutes(DEFAULT_TOKEN_DURATION_MINS);

            let io = TcpStream::connect(&address);

            let token_task = async {
                match token_source {
                    Some(ts) => ts.get(&expiry).await,
                    None => Ok(None),
                }
            };

            let (password, io) = future::try_join(token_task, io).await.map_err(|err| {
                Error::new(ErrorKind::Other, format!("failed to connect: {}", err))
            })?;

            if let Some(pass) = &password {
                validate_length(pass).map_err(|e| {
                    error!(error = %e, "password too long");
                    ErrorKind::InvalidInput
                })?;
            }

            let stream: Pin<Box<dyn ClientIo>> = Box::pin(io);
            Ok((stream, password))
        })
    }

    fn get_tls_source(connection_settings: TcpConnection<SasTokenSource>) -> ClientIoSourceFuture {
        let address = connection_settings.address.clone();
        let token_source = connection_settings.token_source.as_ref().cloned();
        let trust_bundle_source = connection_settings.trust_bundle_source;

        Box::pin(async move {
            let expiry = Utc::now() + chrono::Duration::minutes(DEFAULT_TOKEN_DURATION_MINS);

            let server_root_certificate_task = async {
                match trust_bundle_source {
                    Some(source) => source.get_trust_bundle().await,
                    None => Ok(None),
                }
            };

            let token_task = async {
                match token_source {
                    Some(ts) => ts.get(&expiry).await,
                    None => Ok(None),
                }
            };

            let io = TcpStream::connect(address.clone());

            let (server_root_certificate, password, stream) =
                future::try_join3(server_root_certificate_task, token_task, io)
                    .await
                    .map_err(|err| {
                        Error::new(ErrorKind::Other, format!("failed to connect: {}", err))
                    })?;

            if let Some(pass) = password.as_ref() {
                validate_length(pass).map_err(|e| {
                    error!(error = %e, "password too long");
                    ErrorKind::InvalidInput
                })?;
            }

            let hostname = address.split(':').next().unwrap_or(&address);
            let mut ssl = SslConnector::builder(SslMethod::tls())
                .map(|mut builder| {
                    if let Some(trust_bundle) = server_root_certificate {
                        X509::stack_from_pem(trust_bundle.as_bytes())
                            .map(|mut certs| {
                                while let Some(ca) = certs.pop() {
                                    builder.cert_store_mut().add_cert(ca).ok();
                                }
                            })
                            .ok();
                    }

                    builder.build()
                })
                .and_then(|conn| conn.configure())
                .and_then(|config| config.into_ssl(hostname))
                .and_then(|ssl| SslStream::new(ssl, stream))
                .map_err(|e| Error::new(ErrorKind::NotConnected, e))?;

            Pin::new(&mut ssl)
                .connect()
                .await
                .map_err(|e| Error::new(ErrorKind::NotConnected, e))?;

            let stream: Pin<Box<dyn ClientIo>> = Box::pin(ssl);
            Ok((stream, password))
        })
    }
}

fn validate_length(id: &str) -> Result<(), TryFromIntError> {
    let _: u16 = id.len().try_into()?;

    Ok(())
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
            percent_encode(provider_settings.device_id().as_bytes(), IOTHUB_ENCODE_SET),
            percent_encode(provider_settings.module_id().as_bytes(), IOTHUB_ENCODE_SET)
        )
        .to_lowercase();
        let resource_uri = percent_encode(audience.as_bytes(), IOTHUB_ENCODE_SET);
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
                let token = self.generate_sas_token(provider_settings, expiry).await?;
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
                Some(cert.clone())
            }
            Credentials::PlainText(cred_settings) => {
                cred_settings.trust_bundle().map(str::to_string)
            }
            _ => None,
        };

        Ok(certificate)
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(untagged)]
pub enum Credentials {
    Anonymous(String),
    PlainText(AuthenticationSettings),
    Provider(CredentialProviderSettings),
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct CredentialProviderSettings {
    iothub_hostname: String,
    gateway_hostname: String,
    device_id: String,
    module_id: String,
    generation_id: String,
    workload_uri: String,
}

impl CredentialProviderSettings {
    pub fn new(
        iothub_hostname: impl Into<String>,
        gateway_hostname: impl Into<String>,
        device_id: impl Into<String>,
        module_id: impl Into<String>,
        generation_id: impl Into<String>,
        workload_uri: impl Into<String>,
    ) -> Self {
        CredentialProviderSettings {
            iothub_hostname: iothub_hostname.into(),
            gateway_hostname: gateway_hostname.into(),
            device_id: device_id.into(),
            module_id: module_id.into(),
            generation_id: generation_id.into(),
            workload_uri: workload_uri.into(),
        }
    }

    pub fn iothub_hostname(&self) -> &str {
        &self.iothub_hostname
    }

    pub fn gateway_hostname(&self) -> &str {
        &self.gateway_hostname
    }

    pub fn device_id(&self) -> &str {
        &self.device_id
    }

    pub fn module_id(&self) -> &str {
        &self.module_id
    }

    pub fn generation_id(&self) -> &str {
        &self.generation_id
    }

    pub fn workload_uri(&self) -> &str {
        &self.workload_uri
    }
}

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct AuthenticationSettings {
    client_id: String,
    username: String,
    password: String,
    trust_bundle: Option<String>,
}

impl AuthenticationSettings {
    pub fn new(
        client_id: impl Into<String>,
        username: impl Into<String>,
        password: impl Into<String>,
        trust_bundle: Option<String>,
    ) -> Self {
        Self {
            client_id: client_id.into(),
            username: username.into(),
            password: password.into(),
            trust_bundle,
        }
    }

    pub fn client_id(&self) -> &str {
        &self.client_id
    }

    pub fn username(&self) -> &str {
        &self.username
    }

    pub fn password(&self) -> &str {
        &self.password
    }

    pub fn trust_bundle(&self) -> Option<&str> {
        self.trust_bundle.as_deref()
    }
}
