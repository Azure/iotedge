// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Connect {
    pub workload_uri: url::Url,
    pub management_uri: url::Url,
}

impl Connect {
    pub fn workload_uri(&self) -> &url::Url {
        &self.workload_uri
    }

    pub fn management_uri(&self) -> &url::Url {
        &self.management_uri
    }
}

impl Default for Connect {
    fn default() -> Self {
        let workload_uri = std::env::var("IOTEDGE_CONNECT_WORKLOAD_URI")
            .unwrap_or_else(|_| "unix:///var/run/iotedge/workload.sock".to_string());
        let management_uri = std::env::var("IOTEDGE_CONNECT_MANAGEMENT_URI")
            .unwrap_or_else(|_| "unix:///var/run/iotedge/mgmt.sock".to_string());

        Connect {
            workload_uri: workload_uri.parse().expect("failed to parse workload uri"),
            management_uri: management_uri
                .parse()
                .expect("failed to parse management uri"),
        }
    }
}

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Listen {
    pub workload_uri: url::Url,
    pub management_uri: url::Url,
    #[serde(default)]
    pub min_tls_version: MinTlsVersion,
}

impl Listen {
    pub fn legacy_workload_uri(&self) -> &url::Url {
        &self.workload_uri
    }

    pub fn workload_mnt_uri(home_dir: &str) -> String {
        "unix://".to_string() + home_dir + "/mnt"
    }

    pub fn workload_uri(home_dir: &str, module_id: &str) -> Result<url::Url, url::ParseError> {
        url::Url::parse(&("unix://".to_string() + home_dir + "/mnt/" + module_id + ".sock"))
    }

    pub fn get_workload_systemd_socket_name() -> String {
        "aziot-edged.workload.socket".to_string()
    }

    pub fn get_management_systemd_socket_name() -> String {
        "aziot-edged.mgmt.socket".to_string()
    }

    pub fn management_uri(&self) -> &url::Url {
        &self.management_uri
    }

    pub fn min_tls_version(&self) -> MinTlsVersion {
        self.min_tls_version
    }
}

impl Default for Listen {
    fn default() -> Self {
        let workload_uri = std::env::var("IOTEDGE_LISTEN_WORKLOAD_URI")
            .unwrap_or_else(|_| "fd://aziot-edged.workload.socket".to_string());
        let management_uri = std::env::var("IOTEDGE_LISTEN_MANAGEMENT_URI")
            .unwrap_or_else(|_| "fd://aziot-edged.mgmt.socket".to_string());

        Listen {
            workload_uri: workload_uri.parse().expect("failed to parse workload uri"),
            management_uri: management_uri
                .parse()
                .expect("failed to parse management uri"),
            min_tls_version: MinTlsVersion::default(),
        }
    }
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub enum MinTlsVersion {
    #[default]
    Tls12,
    Tls13,
}

impl std::fmt::Display for MinTlsVersion {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            MinTlsVersion::Tls12 => write!(f, "TLS 1.2"),
            MinTlsVersion::Tls13 => write!(f, "TLS 1.3"),
        }
    }
}

impl std::str::FromStr for MinTlsVersion {
    type Err = String;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s.to_lowercase().as_ref() {
            "tls12" | "tls1.2" | "tls1_2" | "tlsv12" => Ok(MinTlsVersion::Tls12),
            "tls13" | "tls1.3" | "tls1_3" | "tlsv13" => Ok(MinTlsVersion::Tls13),
            _ => Err(format!("Unsupported TLS protocol version: {s}")),
        }
    }
}

impl<'de> serde::Deserialize<'de> for MinTlsVersion {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::de::Deserializer<'de>,
    {
        struct Visitor;

        impl<'de> serde::de::Visitor<'de> for Visitor {
            type Value = MinTlsVersion;

            fn expecting(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                write!(formatter, r#"one of "tls1.2", "tls1.3""#)
            }

            fn visit_str<E>(self, v: &str) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                v.parse().map_err(|_err| {
                    serde::de::Error::invalid_value(serde::de::Unexpected::Str(v), &self)
                })
            }
        }

        deserializer.deserialize_str(Visitor)
    }
}

impl serde::ser::Serialize for MinTlsVersion {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::ser::Serializer,
    {
        serializer.serialize_str(match self {
            MinTlsVersion::Tls12 => "tls1.2",
            MinTlsVersion::Tls13 => "tls1.3",
        })
    }
}

#[cfg(test)]
mod tests {
    use super::MinTlsVersion;
    use std::str::FromStr;
    use test_case::test_case;

    #[test_case("tls12", MinTlsVersion::Tls12; "when tls12 provided")]
    #[test_case("tls1.2", MinTlsVersion::Tls12; "when tls12 with dot provided")]
    #[test_case("tls1_2", MinTlsVersion::Tls12; "when tls12 with underscore provided")]
    #[test_case("Tlsv12" , MinTlsVersion::Tls12; "when Tlsv12 provided")]
    #[test_case("TLS12", MinTlsVersion::Tls12; "when uppercase TLS12 Provided")]
    #[test_case("tls13", MinTlsVersion::Tls13; "when tls13 provided")]
    #[test_case("tls1.3", MinTlsVersion::Tls13; "when tls13 with dot provided")]
    #[test_case("tls1_3", MinTlsVersion::Tls13; "when tls13 with underscore provided")]
    #[test_case("Tlsv13" , MinTlsVersion::Tls13; "when Tlsv13 provided")]
    #[test_case("TLS13", MinTlsVersion::Tls13; "when uppercase TLS13 Provided")]
    fn parse_min_tls_version(value: &str, expected: MinTlsVersion) {
        let actual = MinTlsVersion::from_str(value);
        assert_eq!(actual, Ok(expected));
    }

    #[test_case(""; "when empty string provided")]
    #[test_case("Sslv3"; "when unsupported version provided")]
    #[test_case("TLS2"; "when non-existing version provided")]
    fn parse_min_tls_version_err(value: &str) {
        let actual = MinTlsVersion::from_str(value);
        assert_eq!(
            actual,
            Err(format!("Unsupported TLS protocol version: {value}"))
        );
    }
}
