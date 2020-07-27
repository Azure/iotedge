use std::fs::File;
use std::io::Read;
use std::path::Path;

use serde::Deserialize;
use zeroize::Zeroize;

fn default_storage_location() -> String {
    "/etc/secretstore".to_string()
}

fn default_socket_directory() -> String {
    "/var/tmp".to_string()
}

fn default_encryption_source() -> EncryptionSource {
    EncryptionSource::Automatic
}

fn default_device_cert() -> String {
    "".to_string()
}

fn default_device_cert_pk() -> String {
    "".to_string()
}

fn default_trusted_certs() -> Vec<String> {
    Vec::new()
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum EncryptionSource {
    Automatic,
    FixedKey(String),
    RemoteKey(String)
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StorePermissions {
    Read,
    ReadWrite
}

#[derive(Clone, Debug, Deserialize, Zeroize)]
#[zeroize(drop)]
pub struct AADCredentials {
    pub tenant_id: String,
    pub client_id: String,
    pub client_secret: String
}

#[derive(Debug, Deserialize)]
pub struct Principal {
    pub name: String,
    pub uid: u32,
    pub permissions: StorePermissions
}

#[derive(Debug, Deserialize)]
pub struct LocalSettings {
    #[serde(default = "default_storage_location")]
    pub storage_location: String,
    #[serde(default = "default_socket_directory")]
    pub socket_directory: String,
    #[serde(default = "default_encryption_source")]
    pub encryption_source: EncryptionSource
}

#[derive(Debug, Deserialize)]
pub struct Certificates {
    #[serde(default = "default_device_cert")]
    pub device_cert: String,
    #[serde(default = "default_device_cert_pk")]
    pub device_cert_pk: String,
    #[serde(default = "default_trusted_certs")]
    pub trusted_certs: Vec<String>
}

#[derive(Debug, Deserialize)]
pub struct Configuration {
    pub credentials: AADCredentials,
    pub principal: Vec<Principal>,
    pub local: LocalSettings,
    pub certificates: Certificates
}

pub fn load(path: &Path) -> Configuration {
    let mut conf = File::open(path).unwrap();
    let mut buf = String::new();
    conf.read_to_string(&mut buf).unwrap();
    toml::from_str(&buf).unwrap()
}
