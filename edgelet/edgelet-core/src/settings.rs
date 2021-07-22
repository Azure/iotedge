// Copyright (c) Microsoft. All rights reserved.

use std::cmp::Ordering;
use std::convert::TryInto;
use std::fmt::Display;
use std::path::{Path, PathBuf};
use std::str::FromStr;

use serde::{Deserialize, Deserializer, Serialize, Serializer};
use url::{ParseError, Url};

use crate::module::ModuleSpec;

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Connect {
    pub workload_uri: Url,
    pub management_uri: Url,
}

impl Connect {
    pub fn workload_uri(&self) -> &Url {
        &self.workload_uri
    }

    pub fn management_uri(&self) -> &Url {
        &self.management_uri
    }
}

impl Default for Connect {
    // Clippy wants us to use `option_env!("...").unwrap_or("...")` but that can't be used in consts.
    #[allow(clippy::option_if_let_else)]
    fn default() -> Self {
        const DEFAULT_MANAGEMENT_URI: &str =
            if let Some(value) = option_env!("IOTEDGE_CONNECT_MANAGEMENT_URI") {
                value
            } else {
                "unix:///var/run/iotedge/mgmt.sock"
            };
        const DEFAULT_WORKLOAD_URI: &str =
            if let Some(value) = option_env!("IOTEDGE_CONNECT_WORKLOAD_URI") {
                value
            } else {
                "unix:///var/run/iotedge/workload.sock"
            };

        Connect {
            workload_uri: DEFAULT_WORKLOAD_URI
                .parse()
                .expect("hard-coded url::Url must parse successfully"),
            management_uri: DEFAULT_MANAGEMENT_URI
                .parse()
                .expect("hard-coded url::Url must parse successfully"),
        }
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Listen {
    pub workload_uri: Url,
    pub management_uri: Url,
    #[serde(default = "Protocol::default")]
    pub min_tls_version: Protocol,
}

impl Listen {
    pub fn legacy_workload_uri(&self) -> &Url {
        &self.workload_uri
    }

    pub fn workload_mnt_uri(home_dir: &str) -> String {
        "unix://".to_string() + home_dir + "/mnt"
    }

    pub fn workload_uri(home_dir: &str, module_id: &str) -> Result<Url, ParseError> {
        Url::parse(&("unix://".to_string() + home_dir + "/mnt/" + module_id + ".sock"))
    }

    pub fn management_uri(&self) -> &Url {
        &self.management_uri
    }

    pub fn min_tls_version(&self) -> Protocol {
        self.min_tls_version
    }
}

impl Default for Listen {
    // Clippy wants us to use `option_env!("...").unwrap_or("...")` but that can't be used in consts.
    #[allow(clippy::option_if_let_else)]
    fn default() -> Self {
        const DEFAULT_MANAGEMENT_URI: &str =
            if let Some(value) = option_env!("IOTEDGE_LISTEN_MANAGEMENT_URI") {
                value
            } else {
                "fd://aziot-edged.mgmt.socket"
            };
        const DEFAULT_WORKLOAD_URI: &str =
            if let Some(value) = option_env!("IOTEDGE_LISTEN_WORKLOAD_URI") {
                value
            } else {
                "fd://aziot-edged.workload.socket"
            };

        Listen {
            workload_uri: DEFAULT_WORKLOAD_URI
                .parse()
                .expect("hard-coded url::Url must parse successfully"),
            management_uri: DEFAULT_MANAGEMENT_URI
                .parse()
                .expect("hard-coded url::Url must parse successfully"),
            min_tls_version: Default::default(),
        }
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
        struct Visitor;

        impl<'de> serde::de::Visitor<'de> for Visitor {
            type Value = Protocol;

            fn expecting(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                write!(formatter, r#"one of "tls1.0", "tls1.1", "tls1.2""#)
            }

            fn visit_str<E>(self, v: &str) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(v.parse().map_err(|_err| {
                    serde::de::Error::invalid_value(serde::de::Unexpected::Str(v), &self)
                })?)
            }
        }

        deserializer.deserialize_str(Visitor)
    }
}

impl Serialize for Protocol {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        serializer.serialize_str(match self {
            Protocol::Tls10 => "tls1.0",
            Protocol::Tls11 => "tls1.1",
            Protocol::Tls12 => "tls1.2",
        })
    }
}

#[derive(Clone, Copy, Debug)]
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

impl<'de> serde::Deserialize<'de> for RetryLimit {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        struct Visitor;

        impl<'de> serde::de::Visitor<'de> for Visitor {
            type Value = RetryLimit;

            fn expecting(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                f.write_str(r#""infinite" or u32"#)
            }

            fn visit_str<E>(self, s: &str) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                if s.eq_ignore_ascii_case("infinite") {
                    Ok(RetryLimit::Infinite)
                } else {
                    Err(serde::de::Error::invalid_value(
                        serde::de::Unexpected::Str(s),
                        &self,
                    ))
                }
            }

            fn visit_i64<E>(self, v: i64) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(RetryLimit::Num(
                    v.try_into().map_err(serde::de::Error::custom)?,
                ))
            }

            fn visit_u8<E>(self, v: u8) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(RetryLimit::Num(v.into()))
            }

            fn visit_u16<E>(self, v: u16) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(RetryLimit::Num(v.into()))
            }

            fn visit_u32<E>(self, v: u32) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(RetryLimit::Num(v))
            }

            fn visit_u64<E>(self, v: u64) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(RetryLimit::Num(
                    v.try_into().map_err(serde::de::Error::custom)?,
                ))
            }
        }

        deserializer.deserialize_any(Visitor)
    }
}

impl serde::Serialize for RetryLimit {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        match *self {
            RetryLimit::Infinite => serializer.serialize_str("infinite"),
            RetryLimit::Num(num) => serializer.serialize_u32(num),
        }
    }
}

#[derive(Clone, Debug, Default, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct WatchdogSettings {
    #[serde(default)]
    pub max_retries: RetryLimit,
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
    fn connect(&self) -> &Connect;
    fn listen(&self) -> &Listen;
    fn homedir(&self) -> &Path;
    fn watchdog(&self) -> &WatchdogSettings;
    fn endpoints(&self) -> &Endpoints;
    fn edge_ca_cert(&self) -> Option<&str>;
    fn edge_ca_key(&self) -> Option<&str>;
    fn trust_bundle_cert(&self) -> Option<&str>;
    fn manifest_trust_bundle_cert(&self) -> Option<&str>;
    fn auto_reprovisioning_mode(&self) -> &AutoReprovisioningMode;
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub enum AutoReprovisioningMode {
    Dynamic,
    AlwaysOnStartup,
    OnErrorOnly,
}

impl Default for AutoReprovisioningMode {
    fn default() -> Self {
        AutoReprovisioningMode::Dynamic
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Settings<T> {
    pub hostname: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub edge_ca_cert: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub edge_ca_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub trust_bundle_cert: Option<String>,

    #[serde(default = "AutoReprovisioningMode::default")]
    pub auto_reprovisioning_mode: AutoReprovisioningMode,

    pub homedir: PathBuf,

    #[serde(default = "true_func")]
    pub allow_elevated_docker_permissions: bool,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub manifest_trust_bundle_cert: Option<String>,

    pub agent: ModuleSpec<T>,

    pub connect: Connect,
    pub listen: Listen,

    #[serde(default)]
    pub watchdog: WatchdogSettings,

    /// Map of service names to endpoint URIs.
    ///
    /// Only configurable in debug builds for the sake of tests.
    #[serde(default, skip_serializing)]
    #[cfg_attr(not(debug_assertions), serde(skip_deserializing))]
    pub endpoints: Endpoints,
}

// Serde default requires a function: https://github.com/serde-rs/serde/issues/1030
fn true_func() -> bool {
    true
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

    fn edge_ca_cert(&self) -> Option<&str> {
        self.edge_ca_cert.as_deref()
    }

    fn edge_ca_key(&self) -> Option<&str> {
        self.edge_ca_key.as_deref()
    }

    fn trust_bundle_cert(&self) -> Option<&str> {
        self.trust_bundle_cert.as_deref()
    }

    fn manifest_trust_bundle_cert(&self) -> Option<&str> {
        self.manifest_trust_bundle_cert.as_deref()
    }

    fn auto_reprovisioning_mode(&self) -> &AutoReprovisioningMode {
        &self.auto_reprovisioning_mode
    }
}

#[derive(Clone, Debug, PartialEq, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Endpoints {
    aziot_certd_url: Url,
    aziot_keyd_url: Url,
    aziot_identityd_url: Url,
}

impl Default for Endpoints {
    fn default() -> Self {
        Endpoints {
            aziot_certd_url: Url::parse("unix:///run/aziot/certd.sock").expect("Url parse failed"),
            aziot_keyd_url: Url::parse("unix:///run/aziot/keyd.sock").expect("Url parse failed"),
            aziot_identityd_url: Url::parse("unix:///run/aziot/identityd.sock")
                .expect("Url parse failed"),
        }
    }
}

impl Endpoints {
    pub fn aziot_certd_url(&self) -> &Url {
        &self.aziot_certd_url
    }

    pub fn aziot_keyd_url(&self) -> &Url {
        &self.aziot_keyd_url
    }

    pub fn aziot_identityd_url(&self) -> &Url {
        &self.aziot_identityd_url
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
}
