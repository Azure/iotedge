// Copyright (c) Microsoft. All rights reserved.

use std::path::{Path, PathBuf};

use config::{Config, File, FileFormat};
use failure::ResultExt;
use log::info;
use serde_derive::Deserialize;
use url::Url;

use crate::{Error, ErrorKind, InitializeErrorReason};

pub const DEFAULTS: &str = include_str!("../config/default.yaml");

pub const DEFAULT_SETTINGS_FILEPATH: &str = "/etc/iotedge-proxy/config.yaml";

const TOKEN_FILEPATH: &str = "/var/run/secrets/kubernetes.io/serviceaccount/token";

#[derive(Clone, Debug, Deserialize)]
pub struct Settings {
    // services we will proxy.
    services: Vec<ServiceSettings>,
    // optional api declaration for liveness/readiness probe
    api: Option<ApiSettings>,
}

impl Settings {
    pub fn new(path: Option<&Path>) -> Result<Settings, Error> {
        let mut config = Config::default();
        config
            .merge(File::from_str(DEFAULTS, FileFormat::Yaml))
            .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;

        if let Some(path) = path {
            info!("Using config file: {}", path.display());
            config
                .merge(File::from(path))
                .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;
        } else {
            info!("Using default configuration");
        }

        let settings = convert(config)?;
        Ok(settings)
    }

    pub fn services(&self) -> &Vec<ServiceSettings> {
        &self.services
    }

    pub fn api(&self) -> Option<&ApiSettings> {
        self.api.as_ref()
    }
}

fn convert(config: Config) -> Result<Settings, Error> {
    let settings: Settings = config
        .try_into()
        .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;

    for settings in settings.services() {
        if settings.entrypoint().scheme() != "http" {
            return Err(ErrorKind::Initialize(
                InitializeErrorReason::LoadSettingsUnsupportedSchema(
                    settings.entrypoint().as_str().to_owned(),
                ),
            )
            .into());
        }

        if settings.backend().scheme() != "https" {
            return Err(ErrorKind::Initialize(
                InitializeErrorReason::LoadSettingsUnsupportedSchema(
                    settings.backend().as_str().to_owned(),
                ),
            )
            .into());
        }
    }

    Ok(settings)
}

#[derive(Clone, Debug, Deserialize)]
pub struct ServiceSettings {
    name: String,

    #[serde(with = "url_serde")]
    entrypoint: Url,

    #[serde(with = "url_serde")]
    backend: Url,

    certificate: Option<PathBuf>,

    #[serde(default = "default_token")]
    token: PathBuf,
}

fn default_token() -> PathBuf {
    Path::new(TOKEN_FILEPATH).to_path_buf()
}

impl ServiceSettings {
    pub fn new(
        name: String,
        entrypoint: Url,
        backend: Url,
        cert: Option<&Path>,
        token: &Path,
    ) -> Self {
        ServiceSettings {
            name,
            entrypoint,
            backend,
            certificate: cert.map(Path::to_path_buf),
            token: token.to_path_buf(),
        }
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn entrypoint(&self) -> &Url {
        &self.entrypoint
    }

    pub fn backend(&self) -> &Url {
        &self.backend
    }

    pub fn certificate(&self) -> Option<&Path> {
        self.certificate.as_ref().map(AsRef::as_ref)
    }

    pub fn token(&self) -> &Path {
        &self.token
    }
}

#[derive(Clone, Debug, Deserialize)]
pub struct ApiSettings {
    #[serde(with = "url_serde")]
    entrypoint: Url,
}

impl ApiSettings {
    pub fn new(entrypoint: Url) -> Self {
        ApiSettings { entrypoint }
    }

    pub fn entrypoint(&self) -> &Url {
        &self.entrypoint
    }
}

#[cfg(test)]
mod tests {
    use std::path::Path;

    use url::Url;

    use crate::settings::TOKEN_FILEPATH;
    use crate::{ErrorKind, InitializeErrorReason, Settings};

    #[test]
    fn it_loads_defaults() {
        let settings = Settings::new(None).unwrap();

        assert!(settings.services().is_empty());
        assert!(settings.api().is_none())
    }

    #[test]
    fn it_overrides_defaults() {
        let settings = Settings::new(Some(Path::new("test/sample.yaml"))).unwrap();

        assert_eq!(settings.services().len(), 3);

        assert_eq!(settings.services()[0].name(), "management");
        assert_eq!(
            settings.services()[0].entrypoint(),
            &Url::parse("http://localhost:3000").unwrap()
        );
        assert_eq!(
            settings.services()[0].backend(),
            &Url::parse("https://iotedged:35000").unwrap()
        );
        assert_eq!(
            settings.services()[0].certificate().unwrap(),
            Path::new("management.pem")
        );
        assert_eq!(settings.services()[0].token(), Path::new(TOKEN_FILEPATH));

        assert_eq!(settings.services()[1].name(), "workload");
        assert_eq!(
            settings.services()[1].entrypoint(),
            &Url::parse("http://localhost:3001").unwrap()
        );
        assert_eq!(
            settings.services()[1].backend(),
            &Url::parse("https://iotedged:35001").unwrap()
        );
        assert_eq!(
            settings.services()[1].certificate().unwrap(),
            Path::new("workload.pem")
        );
        assert_eq!(settings.services()[2].name(), "no cert provided");
        assert_eq!(
            settings.services()[2].entrypoint(),
            &Url::parse("http://localhost:3002").unwrap()
        );
        assert_eq!(
            settings.services()[2].backend(),
            &Url::parse("https://iotedged:35002").unwrap()
        );

        assert_eq!(
            settings.api().unwrap().entrypoint(),
            &Url::parse("http://example:443").unwrap()
        );
        assert_eq!(settings.services()[1].token(), Path::new("token"));
    }

    #[test]
    fn it_fails_to_load_invalid_settings() {
        let err = Settings::new(Some(Path::new("test/invalid.yaml"))).unwrap_err();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::LoadSettings)
        );
    }

    #[test]
    fn it_fails_to_load_settings_with_invalid_url() {
        let err = Settings::new(Some(Path::new("test/invalid.url.yaml"))).unwrap_err();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::LoadSettings)
        );
    }

    #[test]
    fn it_allows_only_http_for_entrypoint() {
        let err = Settings::new(Some(Path::new("test/unsupported.entrypoint.yaml"))).unwrap_err();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::LoadSettingsUnsupportedSchema(
                "https://localhost:3000/".to_owned()
            ))
        );
    }

    #[test]
    fn it_allows_only_https_for_backend() {
        let err = Settings::new(Some(Path::new("test/unsupported.backend.yaml"))).unwrap_err();

        assert_eq!(
            err.kind(),
            &ErrorKind::Initialize(InitializeErrorReason::LoadSettingsUnsupportedSchema(
                "http://iotedged:35000/".to_owned()
            ))
        );
    }
}
