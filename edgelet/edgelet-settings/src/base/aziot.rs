// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Copy, Debug, Default, serde::Deserialize, serde::Serialize)]
pub enum AutoReprovisioningMode {
    #[default]
    Dynamic,
    AlwaysOnStartup,
    OnErrorOnly,
}

#[derive(Clone, Debug, Eq, PartialEq, serde::Deserialize, serde::Serialize)]
pub struct Endpoints {
    aziot_certd_url: url::Url,
    aziot_keyd_url: url::Url,
    aziot_identityd_url: url::Url,
}

impl Default for Endpoints {
    fn default() -> Self {
        Endpoints {
            aziot_certd_url: url::Url::parse(&format!(
                "unix://{}/certd.sock",
                option_env!("SOCKET_DIR").unwrap_or("/run/aziot")
            ))
            .expect("cannot fail to parse hardcoded url"),
            aziot_keyd_url: url::Url::parse(&format!(
                "unix://{}/keyd.sock",
                option_env!("SOCKET_DIR").unwrap_or("/run/aziot")
            ))
            .expect("cannot fail to parse hardcoded url"),
            aziot_identityd_url: url::Url::parse(&format!(
                "unix://{}/identityd.sock",
                option_env!("SOCKET_DIR").unwrap_or("/run/aziot")
            ))
            .expect("cannot fail to parse hardcoded url"),
        }
    }
}

impl Endpoints {
    pub fn aziot_certd_url(&self) -> &url::Url {
        &self.aziot_certd_url
    }

    pub fn aziot_keyd_url(&self) -> &url::Url {
        &self.aziot_keyd_url
    }

    pub fn aziot_identityd_url(&self) -> &url::Url {
        &self.aziot_identityd_url
    }
}
