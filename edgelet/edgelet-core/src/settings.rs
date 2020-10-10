// Copyright (c) Microsoft. All rights reserved.

use std::cmp::Ordering;
use std::fmt::Display;
use std::path::{Path, PathBuf};
use std::str::FromStr;

use serde::{de, Deserialize, Deserializer, Serialize, Serializer};
use url::Url;

use crate::module::ModuleSpec;

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Connect {
    workload_uri: Url,
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
    workload_uri: Url,
    management_uri: Url,
    #[serde(default = "Protocol::default")]
    min_tls_version: Protocol,
}

impl Listen {
    pub fn workload_uri(&self) -> &Url {
        &self.workload_uri
    }

    pub fn management_uri(&self) -> &Url {
        &self.management_uri
    }

    pub fn min_tls_version(&self) -> Protocol {
        self.min_tls_version
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum Protocol {
    Tls10,
    Tls11,
    Tls12,
}

impl Default for Protocol {
    fn default() -> Self {
        Protocol::Tls10
    }
}

impl Display for Protocol {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Protocol::Tls10 => write!(f, "TLS 1.0"),
            Protocol::Tls11 => write!(f, "TLS 1.1"),
            Protocol::Tls12 => write!(f, "TLS 1.2"),
        }
    }
}

impl FromStr for Protocol {
    type Err = String;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s.to_lowercase().as_ref() {
            "tls" | "tls1" | "tls10" | "tls1.0" | "tls1_0" | "tlsv10" => Ok(Protocol::Tls10),
            "tls11" | "tls1.1" | "tls1_1" | "tlsv11" => Ok(Protocol::Tls11),
            "tls12" | "tls1.2" | "tls1_2" | "tlsv12" => Ok(Protocol::Tls12),
            _ => Err(format!("Unsupported TLS protocol version: {}", s)),
        }
    }
}

impl<'de> Deserialize<'de> for Protocol {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let s = String::deserialize(deserializer)?;
        s.parse().map_err(de::Error::custom)
    }
}

impl Serialize for Protocol {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        serializer.serialize_str(&format!("{}", self))
    }
}

#[derive(Clone, Copy, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(untagged)]
pub enum RetryLimit {
    Infinite,
    Num(u32),
}

impl RetryLimit {
    pub fn compare(self, right: u32) -> Ordering {
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
    pub fn max_retries(&self) -> RetryLimit {
        self.max_retries
    }
}

pub trait RuntimeSettings {
    type Config;

    fn agent(&self) -> &ModuleSpec<Self::Config>;
    fn agent_mut(&mut self) -> &mut ModuleSpec<Self::Config>;
    fn hostname(&self) -> &str;
    fn parent_hostname(&self) -> Option<&str>;
    fn connect(&self) -> &Connect;
    fn listen(&self) -> &Listen;
    fn homedir(&self) -> &Path;
    fn watchdog(&self) -> &WatchdogSettings;
    fn endpoints(&self) -> &Endpoints;
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Settings<T> {
    agent: ModuleSpec<T>,
    hostname: String,
    parent_hostname: Option<String>,
    connect: Connect,
    listen: Listen,
    homedir: PathBuf,
    #[serde(default)]
    watchdog: WatchdogSettings,
    endpoints: Endpoints,
}

impl<T> RuntimeSettings for Settings<T>
where
    T: Clone,
{
    type Config = T;

    fn agent(&self) -> &ModuleSpec<T> {
        &self.agent
    }

    fn agent_mut(&mut self) -> &mut ModuleSpec<T> {
        &mut self.agent
    }

    fn hostname(&self) -> &str {
        &self.hostname
    }

    fn parent_hostname(&self) -> Option<&str> {
        self.parent_hostname.as_deref()
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

    fn watchdog(&self) -> &WatchdogSettings {
        &self.watchdog
    }

    fn endpoints(&self) -> &Endpoints {
        &self.endpoints
    }
}

#[derive(Clone, Debug, PartialEq, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Endpoints {
    aziot_certd_uri: Url,
    aziot_keyd_uri: Url,
    aziot_identityd_uri: Url,
}

impl Endpoints {
    pub fn aziot_certd_uri(&self) -> &Url {
        &self.aziot_certd_uri
    }

    pub fn aziot_keyd_uri(&self) -> &Url {
        &self.aziot_keyd_uri
    }

    pub fn aziot_identityd_uri(&self) -> &Url {
        &self.aziot_identityd_uri
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::{FromStr, Protocol};

    #[test_case("tls", Protocol::Tls10; "when tls provided")]
    #[test_case("tls1", Protocol::Tls10; "when tls1 with dot provided")]
    #[test_case("tls10", Protocol::Tls10; "when tls10 provided")]
    #[test_case("tls1.0", Protocol::Tls10; "when tls10 with dot provided")]
    #[test_case("tls1_0", Protocol::Tls10; "when tls10 with underscore provided")]
    #[test_case("Tlsv10" , Protocol::Tls10; "when Tlsv10 provided")]
    #[test_case("TLS10", Protocol::Tls10; "when uppercase TLS10 Provided")]
    #[test_case("tls11", Protocol::Tls11; "when tls11 provided")]
    #[test_case("tls1.1", Protocol::Tls11; "when tls11 with dot provided")]
    #[test_case("tls1_1", Protocol::Tls11; "when tls11 with underscore provided")]
    #[test_case("Tlsv11" , Protocol::Tls11; "when Tlsv11 provided")]
    #[test_case("TLS11", Protocol::Tls11; "when uppercase TLS11 Provided")]
    #[test_case("tls12", Protocol::Tls12; "when tls12 provided")]
    #[test_case("tls1.2", Protocol::Tls12; "when tls12 with dot provided")]
    #[test_case("tls1_2", Protocol::Tls12; "when tls12 with underscore provided")]
    #[test_case("Tlsv12" , Protocol::Tls12; "when Tlsv12 provided")]
    #[test_case("TLS12", Protocol::Tls12; "when uppercase TLS12 Provided")]
    fn it_parses_protocol(value: &str, expected: Protocol) {
        let actual = Protocol::from_str(value);
        assert_eq!(actual, Ok(expected));
    }

    #[test_case(""; "when empty string provided")]
    #[test_case("Sslv3"; "when unsupported version provided")]
    #[test_case("TLS2"; "when non-existing version provided")]
    fn it_fails_to_parse_protocol(value: &str) {
        let actual = Protocol::from_str(value);
        assert_eq!(
            actual,
            Err(format!("Unsupported TLS protocol version: {}", value))
        )
    }

    #[test]
    fn to_string_returns_the_provisioning_type_as_a_string() {
        let ptype = ProvisioningType::Manual(Box::new(Manual::new(
            ManualAuthMethod::DeviceConnectionString(ManualDeviceConnectionString::new(
                "".to_string(),
            )),
        )));
        assert_eq!(
            "manual.device_connection_string",
            ptype.to_string().as_str()
        );

        let ptype = ProvisioningType::Manual(Box::new(Manual::new(ManualAuthMethod::X509(
            ManualX509Auth {
                iothub_hostname: String::default(),
                device_id: String::default(),
                identity_cert: Url::parse("file:///irrelevant").unwrap(),
                identity_pk: Url::parse("file:///irrelevant").unwrap(),
            },
        ))));
        assert_eq!("manual.x509", ptype.to_string().as_str());

        let ptype = ProvisioningType::Dps(Box::new(Dps {
            global_endpoint: Url::parse("http://irrelevant.net").unwrap(),
            scope_id: "irrelevant".to_string(),
            attestation: AttestationMethod::Tpm(TpmAttestationInfo::new("irrelevant".to_string())),
            always_reprovision_on_startup: true,
        }));
        assert_eq!("dps.tpm", ptype.to_string().as_str());

        let ptype = ProvisioningType::Dps(Box::new(Dps {
            global_endpoint: Url::parse("http://irrelevant.net").unwrap(),
            scope_id: "irrelevant".to_string(),
            attestation: AttestationMethod::SymmetricKey(SymmetricKeyAttestationInfo {
                registration_id: "irrelevant".to_string(),
                symmetric_key: "irrelevant".to_string(),
            }),
            always_reprovision_on_startup: true,
        }));
        assert_eq!("dps.symmetric_key", ptype.to_string().as_str());

        let ptype = ProvisioningType::Dps(Box::new(Dps {
            global_endpoint: Url::parse("http://irrelevant.net").unwrap(),
            scope_id: "irrelevant".to_string(),
            attestation: AttestationMethod::X509(X509AttestationInfo {
                registration_id: Some("irrelevant".to_string()),
                identity_cert: Url::parse("file:///irrelevant").unwrap(),
                identity_pk: Url::parse("file:///irrelevant").unwrap(),
            }),
            always_reprovision_on_startup: true,
        }));
        assert_eq!("dps.x509", ptype.to_string().as_str());

        let ptype =
            ProvisioningType::External(External::new(Url::parse("http://irrelevant.net").unwrap()));
        assert_eq!("external", ptype.to_string().as_str());
    }
}
