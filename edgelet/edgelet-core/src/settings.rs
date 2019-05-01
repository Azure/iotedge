// Copyright (c) Microsoft. All rights reserved.

use std::path::{Path, PathBuf};

use url::Url;
use url_serde;

use super::ModuleSpec;

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all = "lowercase")]
pub struct Manual {
    device_connection_string: String,
}

impl Manual {
    pub fn device_connection_string(&self) -> &str {
        &self.device_connection_string
    }
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all = "lowercase")]
pub struct Dps {
    #[serde(with = "url_serde")]
    global_endpoint: Url,
    scope_id: String,
    registration_id: String,
}

impl Dps {
    pub fn global_endpoint(&self) -> &Url {
        &self.global_endpoint
    }

    pub fn scope_id(&self) -> &str {
        &self.scope_id
    }

    pub fn registration_id(&self) -> &str {
        &self.registration_id
    }
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(tag = "source")]
#[serde(rename_all = "lowercase")]
pub enum Provisioning {
    Manual(Manual),
    Dps(Dps),
}

#[derive(Clone, Debug, Deserialize, Serialize)]
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

#[derive(Clone, Debug, Deserialize, Serialize)]
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

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Certificates {
    device_ca_cert: PathBuf,
    device_ca_pk: PathBuf,
    trusted_ca_certs: PathBuf,
}

impl Certificates {
    pub fn device_ca_cert(&self) -> &Path {
        &self.device_ca_cert
    }

    pub fn device_ca_pk(&self) -> &Path {
        &self.device_ca_pk
    }

    pub fn trusted_ca_certs(&self) -> &Path {
        &self.trusted_ca_certs
    }
}

pub trait RuntimeSettings {
    type Config: Send;

    fn provisioning(&self) -> &Provisioning;
    fn agent(&self) -> &ModuleSpec<Self::Config>;
    fn hostname(&self) -> &str;
    fn connect(&self) -> &Connect;
    fn listen(&self) -> &Listen;
    fn homedir(&self) -> &Path;
    fn certificates(&self) -> Option<&Certificates>;
}
