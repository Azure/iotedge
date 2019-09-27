// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::fs;
use std::path::Path;

use failure::{Fail, ResultExt};
use log::info;
use native_tls::{Certificate, Identity, TlsConnector};
use openssl::pkcs12::Pkcs12;
use openssl::pkey::PKey;
use openssl::x509::X509;
use url::Url;

use crate::error::{Error, ErrorKind, KubeConfigErrorReason, Result};
use crate::kube::{Config as KubeConfig, Lookup};

pub trait TokenSource {
    type Error: Fail;

    fn get(&self) -> Result<Option<String>>;
}

#[derive(Clone, Debug)]
pub struct ValueToken(Option<String>);

impl TokenSource for ValueToken {
    type Error = Error;

    fn get(&self) -> Result<Option<String>> {
        Ok(self.0.clone())
    }
}

#[derive(Clone)]
pub struct Config<T> {
    host: Url,
    api_path: String,
    token_source: T,
    tls_connector: TlsConnector,
}

impl<T: TokenSource> Config<T> {
    pub fn new(
        host: Url,
        api_path: String,
        token_source: T,
        tls_connector: TlsConnector,
    ) -> Config<T> {
        Config {
            host,
            api_path,
            token_source,
            tls_connector,
        }
    }

    pub fn in_cluster_config() -> Result<Config<ValueToken>> {
        let (token, connector) = get_token_and_tls_connector()?;
        let host = get_host()?;

        Ok(Config::new(host, "/api".to_string(), token, connector))
    }

    pub fn from_config_file<P>(path: P) -> Result<Config<ValueToken>>
    where
        P: AsRef<Path>,
    {
        let config_contents = fs::read_to_string(&path).context(ErrorKind::KubeConfig(
            KubeConfigErrorReason::LoadConfig(path.as_ref().display().to_string()),
        ))?;
        let kube_config =
            serde_yaml::from_str::<KubeConfig>(&config_contents).context(ErrorKind::KubeConfig(
                KubeConfigErrorReason::LoadConfig(path.as_ref().display().to_string()),
            ))?;

        // if there's no "current context" or if entry is invalid then we bail
        if kube_config.current_context().is_empty() {
            return Err(Error::from(ErrorKind::KubeConfig(
                KubeConfigErrorReason::MissingOrInvalidKubeContext,
            )));
        }
        let current_context = kube_config
            .contexts()
            .get(kube_config.current_context())
            .ok_or_else(|| {
                Error::from(ErrorKind::KubeConfig(
                    KubeConfigErrorReason::MissingOrInvalidKubeContext,
                ))
            })?;
        let cluster = kube_config
            .clusters()
            .get(current_context.cluster())
            .ok_or_else(|| {
                Error::from(ErrorKind::KubeConfig(
                    KubeConfigErrorReason::MissingOrInvalidKubeContext,
                ))
            })?;

        // add the root ca cert to the TLS settings
        let root_ca = Certificate::from_pem(&file_or_data_bytes(
            cluster.certificate_authority(),
            cluster.certificate_authority_data(),
        )?)
        .context(ErrorKind::KubeConfig(
            KubeConfigErrorReason::LoadCertificate,
        ))?;

        let user = kube_config
            .users()
            .get(current_context.user())
            .ok_or_else(|| {
                Error::from(ErrorKind::KubeConfig(KubeConfigErrorReason::MissingUser))
            })?;

        // build a client identity if necessary
        let connector = if let Ok(client_cert) =
            file_or_data_bytes(user.client_certificate(), user.client_certificate_data())
        {
            let identity = identity_from_cert_key(
                user.username().unwrap_or(""),
                &client_cert,
                &file_or_data_bytes(user.client_key(), user.client_key_data())?,
            )?;

            TlsConnector::builder()
                .add_root_certificate(root_ca)
                .identity(identity)
                .build()
                .context(ErrorKind::KubeConfig(KubeConfigErrorReason::Tls))?
        } else {
            TlsConnector::builder()
                .add_root_certificate(root_ca)
                .build()
                .context(ErrorKind::KubeConfig(KubeConfigErrorReason::Tls))?
        };

        let server_url = cluster
            .server()
            .parse::<Url>()
            .context(ErrorKind::KubeConfig(KubeConfigErrorReason::UrlParse(
                cluster.server().to_string(),
            )))?;

        Ok(Config::new(
            server_url,
            "/api".to_string(),
            ValueToken(file_or_data_string(user.token_file(), user.token()).ok()),
            connector,
        ))
    }

    pub fn host(&self) -> &Url {
        &self.host
    }

    pub fn api_path(&self) -> &str {
        self.api_path.as_str()
    }

    pub fn with_api_path(mut self, api_path: String) -> Self {
        self.api_path = api_path;
        self
    }

    pub fn token_source(&self) -> &T {
        &self.token_source
    }

    pub fn tls_connector(&self) -> &TlsConnector {
        &self.tls_connector
    }
}

pub fn get_config() -> Result<Config<ValueToken>> {
    // try to get in-cluster config and if that fails
    // try to load config file from home folder and if that
    // fails, well, then we're out of luck
    match Config::<ValueToken>::in_cluster_config() {
        Ok(val) => {
            info!("Using in-cluster config");
            Ok(val)
        }
        Err(_) => dirs::home_dir()
            .ok_or_else(|| {
                Error::from(ErrorKind::KubeConfig(
                    KubeConfigErrorReason::MissingKubeConfig,
                ))
            })
            .and_then(|mut home_dir| {
                info!("Attempting to use config from ~/.kube/config file.");
                home_dir.push(".kube/config");
                Config::<ValueToken>::from_config_file(home_dir)
            }),
    }
}

fn get_host() -> Result<Url> {
    let host = env::var("KUBERNETES_SERVICE_HOST").context(ErrorKind::KubeConfig(
        KubeConfigErrorReason::MissingEnvVar("KUBERNETES_SERVICE_HOST".into()),
    ))?;

    let url = format!("https://{}:443", host,);
    let url = url
        .parse::<Url>()
        .context(ErrorKind::KubeConfig(KubeConfigErrorReason::UrlParse(url)))?;

    Ok(url)
}

fn get_token_and_tls_connector() -> Result<(ValueToken, TlsConnector)> {
    const TOKEN_FILE: &str = "/var/run/secrets/kubernetes.io/serviceaccount/token";
    const ROOT_CA_FILE: &str = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";

    let token = fs::read_to_string(TOKEN_FILE)
        .context(ErrorKind::KubeConfig(KubeConfigErrorReason::LoadToken))?;

    let cert = fs::read(ROOT_CA_FILE).context(ErrorKind::KubeConfig(
        KubeConfigErrorReason::LoadCertificate,
    ))?;
    let root_ca = Certificate::from_pem(&cert).context(ErrorKind::KubeConfig(
        KubeConfigErrorReason::LoadCertificate,
    ))?;

    let tls_connector = TlsConnector::builder()
        .add_root_certificate(root_ca)
        .build()
        .context(ErrorKind::KubeConfig(KubeConfigErrorReason::Tls))?;

    Ok((ValueToken(Some(token)), tls_connector))
}

fn identity_from_cert_key(user_name: &str, cert: &[u8], key: &[u8]) -> Result<Identity> {
    // NOTE: The crate nativetls does not unfortunately support building an identity
    // from PEM encoded cert and key. So we use OpenSSL to convert PEM format cert/key
    // into a pkcs12 format cert from which we then build the identity. This unfortunately
    // creates a dependency on OpenSSL.
    let key = PKey::private_key_from_pem(&key).context(ErrorKind::KubeConfig(
        KubeConfigErrorReason::LoadCertificate,
    ))?;
    let cert = X509::from_pem(&cert).context(ErrorKind::KubeConfig(
        KubeConfigErrorReason::LoadCertificate,
    ))?;

    let pkcs_cert = Pkcs12::builder()
        .build("", user_name, &key, &cert)
        .context(ErrorKind::KubeConfig(
            KubeConfigErrorReason::LoadCertificate,
        ))?;

    let cert_der = &pkcs_cert.to_der().context(ErrorKind::KubeConfig(
        KubeConfigErrorReason::LoadCertificate,
    ))?;

    let identity = Identity::from_pkcs12(cert_der, "").context(ErrorKind::KubeConfig(
        KubeConfigErrorReason::LoadCertificate,
    ))?;

    Ok(identity)
}

fn file_or_data_bytes(path: Option<&str>, data: Option<&str>) -> Result<Vec<u8>> {
    // the "data" always overrides the file path
    match data {
        Some(data) => Ok(base64::decode(data)
            .context(ErrorKind::KubeConfig(KubeConfigErrorReason::Base64Decode))?),
        None => match path {
            None => Err(Error::from(ErrorKind::KubeConfig(
                KubeConfigErrorReason::MissingData,
            ))),
            Some(path) => Ok(fs::read(path).context(ErrorKind::KubeConfig(
                KubeConfigErrorReason::LoadCertificate,
            ))?),
        },
    }
}

fn file_or_data_string(path: Option<&str>, data: Option<&str>) -> Result<String> {
    // the "data" always overrides the file path
    match data {
        Some(data) => Ok(data.to_string()),
        None => match path {
            None => Err(Error::from(ErrorKind::KubeConfig(
                KubeConfigErrorReason::MissingData,
            ))),
            Some(path) => Ok(fs::read_to_string(path)
                .context(ErrorKind::KubeConfig(KubeConfigErrorReason::LoadToken))?),
        },
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs::File;
    use std::io::Write;
    use tempdir::TempDir;

    #[test]
    fn test_get_host() {
        let env_key = "KUBERNETES_SERVICE_HOST";
        let server = "service1.contoso.com";

        let saved_env = env::var(env_key);
        env::remove_var(env_key);
        assert!(get_host().is_err());
        env::set_var(env_key, "   ");
        assert!(get_host().is_err());
        env::set_var(env_key, "service1.contoso.com");
        let host_url_result = get_host().unwrap();
        assert_eq!(server, host_url_result.host_str().unwrap());
        saved_env
            .iter()
            .for_each(|value| env::set_var(env_key, value));
    }

    #[test]
    fn test_file_or_data_bytes() {
        let tmp_dir = TempDir::new("kube-client-config-test").unwrap();
        let file_path = tmp_dir.path().join("data-file");
        let invalid_path = tmp_dir.path().join("invalid-filename");
        let file_content = b"file content";
        let data_content = b"data content";
        let b64content = base64::encode(data_content);

        {
            let mut tmp_file = File::create(file_path.clone()).unwrap();
            tmp_file.write_all(file_content).expect("write failed");
        }

        let data_from_file = file_or_data_bytes(file_path.to_str(), None).unwrap();
        assert_eq!(data_from_file, file_content);
        let data_from_mem = file_or_data_bytes(None, Some(&b64content)).unwrap();
        assert_eq!(data_from_mem, data_content);
        let data_from_mem = file_or_data_bytes(file_path.to_str(), Some(&b64content)).unwrap();
        assert_eq!(data_from_mem, data_content);
        assert!(file_or_data_bytes(None, None).is_err());
        assert!(file_or_data_bytes(invalid_path.to_str(), None).is_err());
    }

    #[test]
    fn test_file_or_data_string() {
        let tmp_dir = TempDir::new("kube-client-config-test").unwrap();
        let file_path = tmp_dir.path().join("data-file");
        let invalid_path = tmp_dir.path().join("invalid-filename");
        let file_content = "file content";
        let data_content = "data content";

        {
            let mut tmp_file = File::create(file_path.clone()).unwrap();
            tmp_file
                .write_all(file_content.as_bytes())
                .expect("write failed");
        }

        let data_from_file = file_or_data_string(file_path.to_str(), None).unwrap();
        assert_eq!(data_from_file, file_content);
        let data_from_mem = file_or_data_string(None, Some(data_content)).unwrap();
        assert_eq!(data_from_mem, data_content);
        let data_from_mem = file_or_data_string(file_path.to_str(), Some(data_content)).unwrap();
        assert_eq!(data_from_mem, data_content);
        assert!(file_or_data_string(None, None).is_err());
        assert!(file_or_data_string(invalid_path.to_str(), None).is_err());
    }
}
