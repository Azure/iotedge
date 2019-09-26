// Copyright (c) Microsoft. All rights reserved.

use std::fs;

use failure::ResultExt;
use native_tls::{Certificate, TlsConnector};
use url::Url;

use crate::{Error, ErrorKind, InitializeErrorReason, ServiceSettings};

#[derive(Clone)]
pub struct Config<T>
where
    T: TokenSource,
{
    host: Url,
    token: T,
    tls: TlsConnector,
}

impl<T> Config<T>
where
    T: TokenSource,
{
    pub fn new(host: Url, token: T, tls: TlsConnector) -> Self {
        Config { host, token, tls }
    }

    pub fn host(&self) -> &Url {
        &self.host
    }

    pub fn tls(&self) -> &TlsConnector {
        &self.tls
    }

    pub fn token(&self) -> &impl TokenSource {
        &self.token
    }
}

pub fn get_config(settings: &ServiceSettings) -> Result<Config<ValueToken>, Error> {
    let token = fs::read_to_string(settings.token()).context(ErrorKind::Initialize(
        InitializeErrorReason::ClientConfigReadFile(settings.token().display().to_string()),
    ))?;

    let mut tls = TlsConnector::builder();

    if let Some(path) = settings.certificate() {
        let file = fs::read_to_string(path).context(ErrorKind::Initialize(
            InitializeErrorReason::ClientConfigReadFile(path.display().to_string()),
        ))?;

        let cert = Certificate::from_pem(file.as_bytes())
            .context(ErrorKind::Initialize(InitializeErrorReason::ClientConfig))?;

        tls.add_root_certificate(cert);
    }

    Ok(Config::new(
        settings.backend().clone(),
        ValueToken(Some(token)),
        tls.build()
            .context(ErrorKind::Initialize(InitializeErrorReason::ClientConfig))?,
    ))
}

pub trait TokenSource {
    fn get(&self) -> Option<String>;
}

#[derive(Clone, Debug)]
pub struct ValueToken(pub Option<String>);

impl TokenSource for ValueToken {
    fn get(&self) -> Option<String> {
        self.0.clone()
    }
}

#[cfg(test)]
mod tests {
    use std::fs;

    use tempfile::TempDir;
    use url::Url;

    use crate::proxy::{get_config, TokenSource};
    use crate::tls::CertGenerator;
    use crate::{ErrorKind, InitializeErrorReason, ServiceSettings};

    #[test]
    fn it_loads_config_from_filesystem() {
        let dir = TempDir::new().unwrap();

        let token = dir.path().join("token");
        fs::write(&token, "token").unwrap();

        let cert = dir.path().join("cert.pem");
        CertGenerator::default().cert(&cert).generate().unwrap();

        let settings = ServiceSettings::new(
            "management".to_owned(),
            Url::parse("http://localhost:3000").unwrap(),
            Url::parse("https://iotedged:30000").unwrap(),
            Some(&cert),
            &token,
        );

        let config = get_config(&settings).unwrap();

        assert_eq!(config.token().get(), Some("token".to_string()));
        assert_eq!(
            config.host(),
            &Url::parse("https://iotedged:30000").unwrap()
        );
    }

    #[test]
    fn it_fails_to_load_config_if_token_file_not_exist() {
        let dir = TempDir::new().unwrap();

        let token = dir.path().join("token");
        let cert = dir.path().join("cert.pem");

        let settings = ServiceSettings::new(
            "management".to_owned(),
            Url::parse("http://localhost:3000").unwrap(),
            Url::parse("https://iotedged:30000").unwrap(),
            Some(&cert),
            &token,
        );

        let err = get_config(&settings).err().unwrap();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::ClientConfigReadFile(
                token.display().to_string()
            ))
        );
    }

    #[test]
    fn it_fails_to_load_config_if_cert_not_exist() {
        let dir = TempDir::new().unwrap();

        let token = dir.path().join("token");
        fs::write(&token, "token").unwrap();

        let cert = dir.path().join("cert.pem");

        let settings = ServiceSettings::new(
            "management".to_owned(),
            Url::parse("http://localhost:3000").unwrap(),
            Url::parse("https://iotedged:30000").unwrap(),
            Some(&cert),
            &token,
        );

        let err = get_config(&settings).err().unwrap();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::ClientConfigReadFile(
                cert.display().to_string()
            ))
        );
    }

    #[test]
    fn it_fails_to_load_config_if_cert_is_invalid() {
        let dir = TempDir::new().unwrap();

        let token = dir.path().join("token");
        fs::write(&token, "token").unwrap();

        let cert = dir.path().join("cert.pem");
        fs::write(&cert, "cert").unwrap();

        let settings = ServiceSettings::new(
            "management".to_owned(),
            Url::parse("http://localhost:3000").unwrap(),
            Url::parse("https://iotedged:30000").unwrap(),
            Some(&cert),
            &token,
        );

        let err = get_config(&settings).err().unwrap();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::ClientConfig)
        );
    }
}
