// Copyright (c) Microsoft. All rights reserved.

use regex::Regex;
use url::Url;

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase")]
pub(crate) struct Provisioning {
    #[serde(flatten)]
    pub(crate) provisioning: ProvisioningType,

    #[serde(default)]
    pub(crate) dynamic_reprovisioning: bool,
}

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase", tag = "source")]
pub(crate) enum ProvisioningType {
    Manual(Manual),
    Dps(Dps),
    External(External),
}

#[derive(Debug)]
pub(crate) struct Manual {
    pub(crate) authentication: ManualAuthMethod,
}

impl<'de> serde::Deserialize<'de> for Manual {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, serde::Deserialize)]
        struct Inner {
            #[serde(skip_serializing_if = "Option::is_none")]
            device_connection_string: Option<String>,
            #[serde(skip_serializing_if = "Option::is_none")]
            authentication: Option<ManualAuthMethod>,
        }

        let value: Inner = serde::Deserialize::deserialize(deserializer)?;

        let authentication = match (value.device_connection_string, value.authentication) {
            (Some(_), Some(_)) => {
                return Err(serde::de::Error::custom(
                        "Only one of provisioning.device_connection_string or provisioning.authentication must be set in the config.yaml.",
                    ));
            }
            (Some(device_connection_string), None) => ManualAuthMethod::DeviceConnectionString(
                device_connection_string
                    .parse()
                    .map_err(serde::de::Error::custom)?,
            ),
            (None, Some(auth)) => auth,
            (None, None) => {
                return Err(serde::de::Error::custom(
                    "One of provisioning.device_connection_string or provisioning.authentication must be set in the config.yaml.",
                ));
            }
        };

        Ok(Manual { authentication })
    }
}
#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase", tag = "method")]
pub(crate) enum ManualAuthMethod {
    #[serde(rename = "device_connection_string")]
    DeviceConnectionString(ManualDeviceConnectionString),

    X509(ManualX509Auth),
}

#[derive(Debug)]
pub(crate) struct ManualDeviceConnectionString {
    pub(crate) device_id: String,
    pub(crate) hostname: String,
    pub(crate) shared_access_key: Vec<u8>,
}

impl std::str::FromStr for ManualDeviceConnectionString {
    type Err = String;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        const DEVICEID_KEY: &str = "DeviceId";
        const HOSTNAME_KEY: &str = "HostName";
        const SHAREDACCESSKEY_KEY: &str = "SharedAccessKey";

        const DEVICEID_REGEX: &str = r"^[A-Za-z0-9\-:.+%_#*?!(),=@;$']{1,128}$";
        const HOSTNAME_REGEX: &str = r"^[a-zA-Z0-9_\-\.]+$";

        fn missing_parameter(parameter_name: &str) -> String {
            format!(
                "the connection string is missing required parameter {}",
                parameter_name
            )
        }

        fn malformed_parameter(parameter_name: &str, err: impl std::fmt::Display) -> String {
            format!(
                "the connection string parameter is malformed {}: {}",
                parameter_name, err
            )
        }

        let device_id_regex =
            Regex::new(DEVICEID_REGEX).expect("This hard-coded regex is expected to be valid.");
        let hostname_regex =
            Regex::new(HOSTNAME_REGEX).expect("This hard-coded regex is expected to be valid.");

        let mut device_id = None;
        let mut hostname = None;
        let mut shared_access_key = None;

        for sections in s.split(';') {
            let mut parts = sections.split('=');
            match parts.next() {
                Some(DEVICEID_KEY) => device_id = parts.next().map(String::from),
                Some(HOSTNAME_KEY) => hostname = parts.next().map(String::from),
                Some(SHAREDACCESSKEY_KEY) => shared_access_key = parts.next().map(String::from),
                _ => (), // Ignore extraneous component in the connection string
            }
        }

        let shared_access_key =
            shared_access_key.ok_or_else(|| missing_parameter(SHAREDACCESSKEY_KEY))?;
        if shared_access_key.is_empty() {
            return Err(missing_parameter(SHAREDACCESSKEY_KEY));
        }
        let shared_access_key = base64::decode(&shared_access_key)
            .map_err(|err| malformed_parameter(SHAREDACCESSKEY_KEY, err))?;

        let device_id = device_id.ok_or_else(|| missing_parameter(DEVICEID_KEY))?;
        if !device_id_regex.is_match(&device_id) {
            return Err(missing_parameter(DEVICEID_KEY));
        }

        let hostname = hostname.ok_or_else(|| missing_parameter(HOSTNAME_KEY))?;
        if !hostname_regex.is_match(&hostname) {
            return Err(missing_parameter(HOSTNAME_KEY));
        }

        Ok(ManualDeviceConnectionString {
            device_id,
            hostname,
            shared_access_key,
        })
    }
}

impl<'de> serde::Deserialize<'de> for ManualDeviceConnectionString {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, serde::Deserialize)]
        #[serde(rename_all = "lowercase")]
        struct Inner {
            device_connection_string: String,
        }

        let Inner {
            device_connection_string,
        } = serde::Deserialize::deserialize(deserializer)?;
        let result = device_connection_string
            .parse()
            .map_err(serde::de::Error::custom)?;
        Ok(result)
    }
}

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase")]
pub(crate) struct ManualX509Auth {
    pub(crate) iothub_hostname: String,
    pub(crate) device_id: String,
    pub(crate) identity_cert: Url,
    pub(crate) identity_pk: Url,
}

#[derive(Debug)]
pub(crate) struct Dps {
    pub(crate) global_endpoint: Url,
    pub(crate) scope_id: String,
    pub(crate) attestation: AttestationMethod,
    pub(crate) always_reprovision_on_startup: bool,
}

impl<'de> serde::Deserialize<'de> for Dps {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, serde::Deserialize)]
        struct Inner {
            global_endpoint: Url,
            scope_id: String,
            registration_id: Option<String>,
            #[serde(skip_serializing_if = "Option::is_none")]
            attestation: Option<AttestationMethod>,
            always_reprovision_on_startup: Option<bool>,
        }

        let value: Inner = serde::Deserialize::deserialize(deserializer)?;

        let attestation = match (value.attestation, value.registration_id) {
            (Some(_att), Some(_)) => {
                return Err(serde::de::Error::custom(
                    "Provisioning registration_id has to be set only in attestation",
                ));
            }
            (Some(att), None) => att,
            (None, Some(registration_id)) => {
                AttestationMethod::Tpm(TpmAttestationInfo { registration_id })
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
            always_reprovision_on_startup: value.always_reprovision_on_startup.unwrap_or(false),
        })
    }
}

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase", tag = "method")]
pub(crate) enum AttestationMethod {
    Tpm(TpmAttestationInfo),

    #[serde(rename = "symmetric_key")]
    SymmetricKey(SymmetricKeyAttestationInfo),

    X509(X509AttestationInfo),
}

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase")]
pub(crate) struct TpmAttestationInfo {
    pub(crate) registration_id: String,
}

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase")]
pub(crate) struct SymmetricKeyAttestationInfo {
    pub(crate) registration_id: String,
    #[serde(deserialize_with = "base64_deserialize")]
    pub(crate) symmetric_key: Vec<u8>,
}

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase")]
pub(crate) struct X509AttestationInfo {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub(crate) registration_id: Option<String>,

    pub(crate) identity_cert: Url,

    pub(crate) identity_pk: Url,
}

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase")]
#[allow(dead_code)]
pub(crate) struct External {
    endpoint: Url,
}

fn base64_deserialize<'de, D>(deserializer: D) -> Result<Vec<u8>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    struct Visitor;

    impl<'de> serde::de::Visitor<'de> for Visitor {
        type Value = Vec<u8>;

        fn expecting(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
            write!(formatter, "a base64-encoded string")
        }

        fn visit_str<E>(self, v: &str) -> Result<Self::Value, E>
        where
            E: serde::de::Error,
        {
            base64::decode_config(v, base64::STANDARD).map_err(serde::de::Error::custom)
        }
    }

    deserializer.deserialize_str(Visitor)
}
