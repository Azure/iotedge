// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Copy, Debug, serde::Deserialize, serde::Serialize)]
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

#[derive(Clone, Debug, PartialEq, serde::Deserialize, serde::Serialize)]
pub struct Endpoints {
    aziot_certd_url: url::Url,
    aziot_keyd_url: url::Url,
    aziot_identityd_url: url::Url,
}

impl Default for Endpoints {
    fn default() -> Self {
        Endpoints {
            aziot_certd_url: url::Url::parse("unix:///run/aziot/certd.sock")
                .expect("cannot fail to parse hardcoded url"),
            aziot_keyd_url: url::Url::parse("unix:///run/aziot/keyd.sock")
                .expect("cannot fail to parse hardcoded url"),
            aziot_identityd_url: url::Url::parse("unix:///run/aziot/identityd.sock")
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
