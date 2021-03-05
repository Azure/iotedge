// Copyright (c) Microsoft. All rights reserved.

mod agent;
pub(crate) use agent::*;

mod moby_runtime;
pub(crate) use moby_runtime::*;

mod provisioning;
pub(crate) use provisioning::*;

use std::path::PathBuf;

use url::Url;

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct Config {
    pub(crate) provisioning: Provisioning,

    pub(crate) agent: ModuleSpec,

    pub(crate) hostname: String,
    pub(crate) parent_hostname: Option<String>,

    pub(crate) connect: Connect,
    pub(crate) listen: Listen,

    pub(crate) homedir: PathBuf,

    pub(crate) certificates: Option<Certificates>,

    #[serde(default)]
    pub(crate) watchdog: WatchdogSettings,

    pub(crate) moby_runtime: MobyRuntime,
}

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct Connect {
    pub(crate) management_uri: Url,
    pub(crate) workload_uri: Url,
}

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct Listen {
    pub(crate) management_uri: Url,
    pub(crate) workload_uri: Url,
    #[serde(default = "Protocol::default")]
    pub(crate) min_tls_version: Protocol,
}

#[derive(Clone, Copy, Debug)]
pub(crate) enum Protocol {
    Tls10,
    Tls11,
    Tls12,
}

impl Default for Protocol {
    fn default() -> Self {
        Protocol::Tls10
    }
}

impl<'de> serde::Deserialize<'de> for Protocol {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
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
                match &*v.to_lowercase() {
                    "tls" | "tls1" | "tls10" | "tls1.0" | "tls1_0" | "tlsv10" => {
                        Ok(Protocol::Tls10)
                    }
                    "tls11" | "tls1.1" | "tls1_1" | "tlsv11" => Ok(Protocol::Tls11),
                    "tls12" | "tls1.2" | "tls1_2" | "tlsv12" => Ok(Protocol::Tls12),
                    _ => Err(serde::de::Error::invalid_value(
                        serde::de::Unexpected::Str(v),
                        &self,
                    )),
                }
            }
        }

        deserializer.deserialize_str(Visitor)
    }
}

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct Certificates {
    #[serde(flatten)]
    pub(crate) device_cert: Option<DeviceCertificate>,

    #[serde(default = "default_auto_generated_ca_lifetime_days")]
    pub(crate) auto_generated_ca_lifetime_days: u16,
}

#[derive(Debug, serde_derive::Deserialize)]
pub(crate) struct DeviceCertificate {
    #[serde(deserialize_with = "deserialize_file_uri_or_path")]
    pub(crate) device_ca_cert: Url,
    #[serde(deserialize_with = "deserialize_file_uri_or_path")]
    pub(crate) device_ca_pk: Url,
    #[serde(deserialize_with = "deserialize_file_uri_or_path")]
    pub(crate) trusted_ca_certs: Url,
}

const fn default_auto_generated_ca_lifetime_days() -> u16 {
    1
}

fn deserialize_file_uri_or_path<'de, D>(deserializer: D) -> Result<Url, D::Error>
where
    D: serde::Deserializer<'de>,
{
    struct Visitor;

    impl<'de> serde::de::Visitor<'de> for Visitor {
        type Value = Url;

        fn expecting(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
            f.write_str("file path or file:// URI")
        }

        fn visit_str<E>(self, value: &str) -> Result<Self::Value, E>
        where
            E: serde::de::Error,
        {
            let value = value
                .parse::<Url>()
                .or_else(|err| Url::from_file_path(value).map_err(|()| err));
            match value {
                Ok(value) if value.scheme() == "file" => Ok(value),

                Ok(value) => Err(serde::de::Error::custom(format!(
                    r#"Value has invalid scheme {:?}. Only "file://" URIs are supported."#,
                    value.scheme(),
                ))),

                Err(err) => Err(serde::de::Error::custom(format!(
                    "Could not parse value as a file path or a file:// URI: {}",
                    err,
                ))),
            }
        }
    }

    deserializer.deserialize_str(Visitor)
}

#[derive(Debug, Default, serde_derive::Deserialize)]
pub(crate) struct WatchdogSettings {
    #[serde(default)]
    pub(crate) max_retries: RetryLimit,
}

#[derive(Debug, serde_derive::Deserialize)]
#[serde(untagged)]
pub(crate) enum RetryLimit {
    Infinite,
    Num(u32),
}

impl Default for RetryLimit {
    fn default() -> Self {
        RetryLimit::Infinite
    }
}

pub(crate) const DEFAULT_MGMT_SOCKET_UNIT: &str = "iotedge.mgmt.socket";
pub(crate) const DEFAULT_WORKLOAD_SOCKET_UNIT: &str = "iotedge.socket";

pub(crate) const DEFAULTS: &str = "
provisioning:
  source: 'manual'

agent:
  name: 'edgeAgent'
  type: 'docker'
  env: {}
  config:
    image: 'mcr.microsoft.com/azureiotedge-agent:1.0'
    auth: {}

hostname: 'localhost'

connect:
  management_uri: 'unix:///var/run/iotedge/mgmt.sock'
  workload_uri: 'unix:///var/run/iotedge/workload.sock'

listen:
  management_uri: 'unix:///var/run/iotedge/mgmt.sock'
  workload_uri: 'unix:///var/run/iotedge/workload.sock'

homedir: '/var/lib/iotedge'

moby_runtime:
  uri: 'unix:///var/run/docker.sock'
  network: 'azure-iot-edge'

certificates:
  auto_generated_ca_lifetime_days: 90
";
