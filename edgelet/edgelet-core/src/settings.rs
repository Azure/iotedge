// Copyright (c) Microsoft. All rights reserved.

use std::cmp::Ordering;
use std::path::{Path, PathBuf};

use failure::Fail;
use regex::Regex;
use url::Url;
use url_serde;

use crate::crypto::MemoryKey;
use crate::module::ModuleSpec;

const DEVICEID_KEY: &str = "DeviceId";
const HOSTNAME_KEY: &str = "HostName";
const SHAREDACCESSKEY_KEY: &str = "SharedAccessKey";

const DEVICEID_REGEX: &str = r"^[A-Za-z0-9\-:.+%_#*?!(),=@;$']{1,128}$";
const HOSTNAME_REGEX: &str = r"^[a-zA-Z0-9_\-\.]+$";

/// This is the default connection string
pub const DEFAULT_CONNECTION_STRING: &str = "<ADD DEVICE CONNECTION STRING HERE>";

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(rename_all = "lowercase")]
pub struct Manual {
    device_connection_string: String,
}

impl Manual {
    pub fn new(device_connection_string: String) -> Self {
        Manual {
            device_connection_string,
        }
    }

    pub fn device_connection_string(&self) -> &str {
        &self.device_connection_string
    }

    pub fn parse_device_connection_string(
        &self,
    ) -> Result<(MemoryKey, String, String), ParseManualDeviceConnectionStringError> {
        if self.device_connection_string.is_empty() {
            return Err(ParseManualDeviceConnectionStringError::Empty);
        }

        let mut key = None;
        let mut device_id = None;
        let mut hub = None;

        let parts: Vec<&str> = self.device_connection_string.split(';').collect();
        for p in parts {
            let s: Vec<&str> = p.split('=').collect();
            match s[0] {
                SHAREDACCESSKEY_KEY => key = Some(s[1].to_string()),
                DEVICEID_KEY => device_id = Some(s[1].to_string()),
                HOSTNAME_KEY => hub = Some(s[1].to_string()),
                _ => (), // Ignore extraneous component in the connection string
            }
        }

        let key = key.ok_or(
            ParseManualDeviceConnectionStringError::MissingRequiredParameter(SHAREDACCESSKEY_KEY),
        )?;
        if key.is_empty() {
            return Err(ParseManualDeviceConnectionStringError::MalformedParameter(
                SHAREDACCESSKEY_KEY,
            ));
        }
        let key = MemoryKey::new(base64::decode(&key).map_err(|_| {
            ParseManualDeviceConnectionStringError::MalformedParameter(SHAREDACCESSKEY_KEY)
        })?);

        let device_id = device_id.ok_or(
            ParseManualDeviceConnectionStringError::MissingRequiredParameter(DEVICEID_KEY),
        )?;
        let device_id_regex =
            Regex::new(DEVICEID_REGEX).expect("This hard-coded regex is expected to be valid.");
        if !device_id_regex.is_match(&device_id) {
            return Err(ParseManualDeviceConnectionStringError::MalformedParameter(
                DEVICEID_KEY,
            ));
        }

        let hub = hub.ok_or(
            ParseManualDeviceConnectionStringError::MissingRequiredParameter(HOSTNAME_KEY),
        )?;
        let hub_regex =
            Regex::new(HOSTNAME_REGEX).expect("This hard-coded regex is expected to be valid.");
        if !hub_regex.is_match(&hub) {
            return Err(ParseManualDeviceConnectionStringError::MalformedParameter(
                HOSTNAME_KEY,
            ));
        }

        Ok((key, device_id.to_owned(), hub.to_owned()))
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(tag = "method")]
#[serde(rename_all = "lowercase")]
pub enum AttestationMethod {
    Tpm(TpmAttestationInfo),
    #[serde(rename = "symmetric_key")]
    SymmetricKey(SymmetricKeyAttestationInfo),
    X509(X509AttestationInfo),
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(rename_all = "lowercase")]
pub struct TpmAttestationInfo {
    registration_id: String,
}

impl TpmAttestationInfo {
    pub fn new(registration_id: String) -> Self {
        TpmAttestationInfo { registration_id }
    }

    pub fn registration_id(&self) -> &str {
        &self.registration_id
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(rename_all = "lowercase")]
pub struct SymmetricKeyAttestationInfo {
    registration_id: String,
    symmetric_key: String,
}

impl SymmetricKeyAttestationInfo {
    pub fn registration_id(&self) -> &str {
        &self.registration_id
    }

    pub fn symmetric_key(&self) -> &str {
        &self.symmetric_key
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(rename_all = "lowercase")]
pub struct X509AttestationInfo {
    #[serde(skip_serializing_if = "Option::is_none")]
    registration_id: Option<String>,
    #[serde(with = "url_serde")]
    identity_cert: Url,
    #[serde(with = "url_serde")]
    identity_pk: Url,
}

impl X509AttestationInfo {
    pub fn identity_cert(&self) -> Result<PathBuf, CertificateConfigError> {
        get_path_from_uri(&self.identity_cert, "identity_cert")
    }

    pub fn identity_pk(&self) -> Result<PathBuf, CertificateConfigError> {
        get_path_from_uri(&self.identity_pk, "identity_pk")
    }

    pub fn identity_pk_uri(&self) -> Result<Url, CertificateConfigError> {
        if is_supported_uri(&self.identity_pk) {
            Ok(self.identity_pk.clone())
        } else {
            Err(CertificateConfigError::UnsupportedUri(
                self.identity_pk.to_string(),
                "identity_pk",
            ))
        }
    }

    pub fn identity_cert_uri(&self) -> Result<Url, CertificateConfigError> {
        if is_supported_uri(&self.identity_cert) {
            Ok(self.identity_cert.clone())
        } else {
            Err(CertificateConfigError::UnsupportedUri(
                self.identity_cert.to_string(),
                "identity_cert",
            ))
        }
    }

    pub fn registration_id(&self) -> Option<&str> {
        self.registration_id.as_ref().map(AsRef::as_ref)
    }
}

#[derive(Clone, Debug, serde_derive::Serialize)]
pub struct Dps {
    #[serde(with = "url_serde")]
    global_endpoint: Url,
    scope_id: String,
    attestation: AttestationMethod,
}

impl<'de> serde::Deserialize<'de> for Dps {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, serde_derive::Deserialize)]
        struct Inner {
            #[serde(with = "url_serde")]
            global_endpoint: Url,
            scope_id: String,
            registration_id: Option<String>,
            #[serde(skip_serializing_if = "Option::is_none")]
            attestation: Option<AttestationMethod>,
        }

        let value: Inner = serde::Deserialize::deserialize(deserializer)?;

        let attestation = match (value.attestation, value.registration_id) {
            (Some(_att), Some(_)) => {
                return Err(serde::de::Error::custom(
                    "Provisioning registration_id has to be set only in attestation",
                ));
            }
            (Some(att), None) => att,
            (None, Some(reg_id)) => {
                AttestationMethod::Tpm(TpmAttestationInfo::new(reg_id.to_string()))
            }
            (None, None) => {
                return Err(serde::de::Error::custom(
                    "Provisioning registration_id has to be set",
                ));
            }
        };

        Ok(Dps {
            global_endpoint: value.global_endpoint,
            scope_id: value.scope_id,
            attestation,
        })
    }
}

impl Dps {
    pub fn global_endpoint(&self) -> &Url {
        &self.global_endpoint
    }

    pub fn scope_id(&self) -> &str {
        &self.scope_id
    }

    pub fn attestation(&self) -> &AttestationMethod {
        &self.attestation
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(rename_all = "lowercase")]
pub struct External {
    #[serde(with = "url_serde")]
    endpoint: Url,
}

impl External {
    pub fn new(endpoint: Url) -> Self {
        External { endpoint }
    }

    pub fn endpoint(&self) -> &Url {
        &self.endpoint
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(tag = "source")]
#[serde(rename_all = "lowercase")]
pub enum Provisioning {
    Manual(Manual),
    Dps(Box<Dps>),
    External(External),
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Connect {
    #[serde(with = "url_serde")]
    workload_uri: Url,
    #[serde(with = "url_serde")]
    management_uri: Url,
}

impl Connect {
    pub fn workload_uri(&self) -> &Url {
        &self.workload_uri
    }

    pub fn management_uri(&self) -> &Url {
        &self.management_uri
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Listen {
    #[serde(with = "url_serde")]
    workload_uri: Url,
    #[serde(with = "url_serde")]
    management_uri: Url,
}

impl Listen {
    pub fn workload_uri(&self) -> &Url {
        &self.workload_uri
    }

    pub fn management_uri(&self) -> &Url {
        &self.management_uri
    }
}

#[derive(Clone, Debug, Fail)]
pub enum CertificateConfigError {
    #[fail(
        display = "URI {} is unsupported for '{}'. Please check the config.yaml file.",
        _0, _1
    )]
    UnsupportedUri(String, &'static str),

    #[fail(
        display = "File URI {} is unsupported for '{}'. Please check the config.yaml file.",
        _0, _1
    )]
    UnsupportedFileUri(String, &'static str),

    #[fail(
        display = "Error parsing URI {} specified for '{}'. Please check the config.yaml file.",
        _0, _1
    )]
    InvalidUri(String, &'static str),

    #[fail(
        display = "Invalid file URI {} path specified for '{}'. Please check the config.yaml file.",
        _0, _1
    )]
    InvalidUriFilePath(String, &'static str),
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Certificates {
    device_ca_cert: String,
    device_ca_pk: String,
    trusted_ca_certs: String,
}

fn is_supported_uri(uri: &Url) -> bool {
    if uri.scheme() == "file" && uri.port().is_none() && uri.query().is_none() {
        if let Some(host) = uri.host_str() {
            return "localhost" == host;
        }
        return true;
    }
    false
}

fn get_path_from_uri(uri: &Url, variable: &'static str) -> Result<PathBuf, CertificateConfigError> {
    if is_supported_uri(&uri) {
        let path = uri
            .to_file_path()
            .map_err(|_| CertificateConfigError::InvalidUriFilePath(uri.to_string(), variable))?;
        Ok(path)
    } else {
        Err(CertificateConfigError::UnsupportedFileUri(
            uri.to_string(),
            variable,
        ))
    }
}

fn convert_to_path(
    maybe_path: &str,
    variable: &'static str,
) -> Result<PathBuf, CertificateConfigError> {
    if let Ok(file_uri) = Url::from_file_path(maybe_path) {
        // maybe_path was specified as a valid path not a URI
        get_path_from_uri(&file_uri, variable)
    } else {
        // maybe_path is not a path and could be URI
        let uri = Url::parse(maybe_path)
            .map_err(|_| CertificateConfigError::InvalidUri(String::from(maybe_path), variable))?;
        get_path_from_uri(&uri, variable)
    }
}

fn convert_to_uri(maybe_uri: &str, variable: &'static str) -> Result<Url, CertificateConfigError> {
    if let Ok(uri) = Url::parse(maybe_uri) {
        // maybe_uri was specified as a URI
        if is_supported_uri(&uri) {
            Ok(uri)
        } else {
            Err(CertificateConfigError::UnsupportedUri(
                String::from(maybe_uri),
                variable,
            ))
        }
    } else {
        // maybe_uri was specified as a valid path not a URI
        Url::from_file_path(maybe_uri)
            .map(|uri| {
                if is_supported_uri(&uri) {
                    Ok(uri)
                } else {
                    Err(CertificateConfigError::UnsupportedUri(
                        String::from(maybe_uri),
                        variable,
                    ))
                }
            })
            .map_err(|_| CertificateConfigError::InvalidUri(String::from(maybe_uri), variable))?
    }
}

impl Certificates {
    pub fn device_ca_cert(&self) -> Result<PathBuf, CertificateConfigError> {
        convert_to_path(&self.device_ca_cert, "device_ca_cert")
    }

    pub fn device_ca_pk(&self) -> Result<PathBuf, CertificateConfigError> {
        convert_to_path(&self.device_ca_pk, "device_ca_pk")
    }

    pub fn trusted_ca_certs(&self) -> Result<PathBuf, CertificateConfigError> {
        convert_to_path(&self.trusted_ca_certs, "trusted_ca_certs")
    }

    pub fn device_ca_cert_uri(&self) -> Result<Url, CertificateConfigError> {
        convert_to_uri(&self.device_ca_cert, "device_ca_cert")
    }

    pub fn device_ca_pk_uri(&self) -> Result<Url, CertificateConfigError> {
        convert_to_uri(&self.device_ca_pk, "device_ca_pk")
    }

    pub fn trusted_ca_certs_uri(&self) -> Result<Url, CertificateConfigError> {
        convert_to_uri(&self.trusted_ca_certs, "trusted_ca_certs")
    }
}

#[derive(Clone, Copy, Debug, Fail)]
pub enum ParseManualDeviceConnectionStringError {
    #[fail(
        display = "The Connection String is empty. Please update the config.yaml and provide the IoTHub connection information."
    )]
    Empty,

    #[fail(display = "The Connection String is missing required parameter {}", _0)]
    MissingRequiredParameter(&'static str),

    #[fail(
        display = "The Connection String has a malformed value for parameter {}.",
        _0
    )]
    MalformedParameter(&'static str),
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(untagged)]
pub enum RetryLimit {
    Infinite,
    Num(u32),
}

impl RetryLimit {
    pub fn compare(&self, right: u32) -> Ordering {
        match self {
            RetryLimit::Infinite => Ordering::Greater,
            RetryLimit::Num(n) => n.cmp(&right),
        }
    }
}

impl Default for RetryLimit {
    fn default() -> Self {
        RetryLimit::Infinite
    }
}

#[derive(Clone, Debug, Default, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct WatchdogSettings {
    #[serde(default)]
    max_retries: RetryLimit,
}

impl WatchdogSettings {
    pub fn max_retries(&self) -> &RetryLimit {
        &self.max_retries
    }
}

pub trait RuntimeSettings {
    type Config;

    fn provisioning(&self) -> &Provisioning;
    fn agent(&self) -> &ModuleSpec<Self::Config>;
    fn agent_mut(&mut self) -> &mut ModuleSpec<Self::Config>;
    fn hostname(&self) -> &str;
    fn connect(&self) -> &Connect;
    fn listen(&self) -> &Listen;
    fn homedir(&self) -> &Path;
    fn certificates(&self) -> Option<&Certificates>;
    fn watchdog(&self) -> &WatchdogSettings;
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Settings<T> {
    provisioning: Provisioning,
    agent: ModuleSpec<T>,
    hostname: String,
    connect: Connect,
    listen: Listen,
    homedir: PathBuf,
    certificates: Option<Certificates>,
    #[serde(default)]
    watchdog: WatchdogSettings,
}

impl<T> RuntimeSettings for Settings<T>
where
    T: Clone,
{
    type Config = T;

    fn provisioning(&self) -> &Provisioning {
        &self.provisioning
    }

    fn agent(&self) -> &ModuleSpec<T> {
        &self.agent
    }

    fn agent_mut(&mut self) -> &mut ModuleSpec<T> {
        &mut self.agent
    }

    fn hostname(&self) -> &str {
        &self.hostname
    }

    fn connect(&self) -> &Connect {
        &self.connect
    }

    fn listen(&self) -> &Listen {
        &self.listen
    }

    fn homedir(&self) -> &Path {
        &self.homedir
    }

    fn certificates(&self) -> Option<&Certificates> {
        self.certificates.as_ref()
    }

    fn watchdog(&self) -> &WatchdogSettings {
        &self.watchdog
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_convert_to_path() {
        if cfg!(windows) {
            let expected_path = "C:\\temp\\sample.txt";
            let input = expected_path;
            assert_eq!(
                expected_path,
                convert_to_path(input, "test").unwrap().to_str().unwrap()
            );
            let input = "file:///C:/temp/sample.txt";
            assert_eq!(
                expected_path,
                convert_to_path(input, "test").unwrap().to_str().unwrap()
            );
            let input = "file://localhost/C:/temp/sample.txt";
            assert_eq!(
                expected_path,
                convert_to_path(input, "test").unwrap().to_str().unwrap()
            );
            let input = "file://localhost/C:/temp/../temp/sample.txt";
            assert_eq!(
                expected_path,
                convert_to_path(input, "test").unwrap().to_str().unwrap()
            );
            // oddly this works because the host is null since local drive is specified
            let input = "file://deadhost/C:/temp/sample.txt";
            assert_eq!(
                expected_path,
                convert_to_path(input, "test").unwrap().to_str().unwrap()
            );
            let input = "file://deadhost/temp/sample.txt";
            convert_to_path(input, "test").expect_err("Non localhost host specified");
            let input = "https:///C:/temp/sample.txt";
            convert_to_path(input, "test").expect_err("Non file scheme specified");
            let input = "../sample.txt";
            convert_to_path(input, "test").expect_err("Non absolute path specified");
        } else {
            let expected_path = "/tmp/sample.txt";
            let input = expected_path;
            assert_eq!(
                expected_path,
                convert_to_path(input, "test").unwrap().to_str().unwrap()
            );
            let input = "file:///tmp/sample.txt";
            assert_eq!(
                expected_path,
                convert_to_path(input, "test").unwrap().to_str().unwrap()
            );
            let input = "file://localhost/tmp/sample.txt";
            assert_eq!(
                expected_path,
                convert_to_path(input, "test").unwrap().to_str().unwrap()
            );
            let input = "file:///tmp/../tmp/sample.txt";
            assert_eq!(
                expected_path,
                convert_to_path(input, "test").unwrap().to_str().unwrap()
            );
            let input = "file://deadhost/tmp/sample.txt";
            convert_to_path(input, "test").expect_err("Non localhost host specified");
            let input = "https://localhost/tmp/sample.txt";
            convert_to_path(input, "test").expect_err("Non file scheme specified");
            let input = "../sample.txt";
            convert_to_path(input, "test").expect_err("Non absolute path specified");
        }
    }

    #[test]
    fn test_convert_to_uri() {
        if cfg!(windows) {
            let expected_uri_str = "file:///C:/temp/sample.txt";
            let expected_uri = Url::parse(expected_uri_str).unwrap();
            let input = expected_uri_str;
            assert_eq!(expected_uri, convert_to_uri(input, "test").unwrap());
            let input = "file://localhost/C:/temp/sample.txt";
            assert_eq!(expected_uri, convert_to_uri(input, "test").unwrap());
            let input = "file://localhost/C:/temp/../temp/sample.txt";
            assert_eq!(expected_uri, convert_to_uri(input, "test").unwrap());
            // oddly this works because the host is null since local drive is specified
            let input = "file://deadhost/C:/temp/sample.txt";
            assert_eq!(expected_uri, convert_to_uri(input, "test").unwrap());
            let input = "file://deadhost/temp/sample.txt";
            convert_to_uri(input, "test").expect_err("Non localhost host specified");
            let input = "file://deadhost/temp/sample.txt";
            convert_to_uri(input, "test").expect_err("Non file scheme specified");
            let input = "../sample.txt";
            convert_to_uri(input, "test").expect_err("Non absolute path specified");
        } else {
            let expected_uri_str = "file:///tmp/sample.txt";
            let expected_uri = Url::parse(expected_uri_str).unwrap();
            let input = expected_uri_str;
            assert_eq!(expected_uri, convert_to_uri(input, "test").unwrap());
            let input = "file://localhost/tmp/sample.txt";
            assert_eq!(expected_uri, convert_to_uri(input, "test").unwrap());
            let input = "file:///tmp/../tmp/sample.txt";
            assert_eq!(expected_uri, convert_to_uri(input, "test").unwrap());
            let input = "https://localhost/tmp/sample.txt";
            convert_to_uri(input, "test").expect_err("Non absolute path specified");
            let input = "/tmp/sample.txt";
            assert_eq!(expected_uri, convert_to_uri(input, "test").unwrap());
            let input = "../sample.txt";
            convert_to_uri(input, "test").expect_err("Non absolute path specified");
            let input = "file://deadhost/tmp/sample.txt";
            convert_to_uri(input, "test").expect_err("Non localhost host specified");
        }
    }
}
