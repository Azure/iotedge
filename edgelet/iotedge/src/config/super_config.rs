// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;

use url::Url;

use aziotctl_common::config as common_config;

#[derive(Debug, serde::Deserialize, serde::Serialize)]
pub struct Config {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub trust_bundle_cert: Option<Url>,

    #[serde(default = "edgelet_core::settings::AutoReprovisioningMode::default")]
    pub auto_reprovisioning_mode: edgelet_core::settings::AutoReprovisioningMode,

    /// This property only exists to be able to import IoT Edge 1.1 and earlier's master encryption key
    /// so that Edge modules that used the workload encrypt/decrypt API can continue to decrypt secrets
    /// that they encrypted with 1.1.
    ///
    /// It has to exist here in the super-config because the super-config is the only way
    /// that `iotedge config import` can affect the preloaded keys in the final keyd config
    /// created by `iotedge config apply`, We don't expect users to set this property, or even know about it,
    /// for clean installs; the only thing that sets it is `iotedge config import` and it's not documented
    /// in the super-config template.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub imported_master_encryption_key: Option<std::path::PathBuf>,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub manifest_trust_bundle_cert: Option<Url>,

    #[serde(flatten)]
    pub aziot: aziotctl_common::config::super_config::Config,

    #[serde(default = "default_agent")]
    pub agent: edgelet_core::ModuleSpec<edgelet_docker::DockerConfig>,

    #[serde(default)]
    pub connect: edgelet_core::Connect,
    #[serde(default)]
    pub listen: edgelet_core::Listen,

    #[serde(default)]
    pub watchdog: edgelet_core::WatchdogSettings,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub edge_ca: Option<EdgeCa>,

    #[serde(default)]
    pub moby_runtime: MobyRuntime,
}

pub fn default_agent() -> edgelet_core::ModuleSpec<edgelet_docker::DockerConfig> {
    edgelet_core::ModuleSpec {
        name: "edgeAgent".to_owned(),
        type_: "docker".to_owned(),
        image_pull_policy: Default::default(),
        config: edgelet_docker::DockerConfig {
            image: "mcr.microsoft.com/azureiotedge-agent:1.2".to_owned(),
            image_id: None,
            create_options: docker::models::ContainerCreateBody::new(),
            digest: None,
            auth: None,
        },
        env: Default::default(),
    }
}

#[derive(Debug, serde_derive::Deserialize, serde_derive::Serialize)]
#[serde(untagged)]
pub enum EdgeCa {
    Issued {
        #[serde(flatten)]
        cert: common_config::super_config::CertIssuanceOptions,
    },
    Preloaded {
        cert: Url,
        pk: Url,
    },
    Quickstart {
        auto_generated_edge_ca_expiry_days: u32,
    },
}

#[derive(Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct MobyRuntime {
    pub uri: Url,
    pub network: edgelet_core::MobyNetwork,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub content_trust: Option<ContentTrust>,
}

impl Default for MobyRuntime {
    fn default() -> Self {
        const DEFAULT_URI: &str = "unix:///var/run/docker.sock";

        MobyRuntime {
            uri: DEFAULT_URI
                .parse()
                .expect("hard-coded url::Url must parse successfully"),
            network: edgelet_core::MobyNetwork::Name(edgelet_core::DEFAULT_NETWORKID.to_owned()),
            content_trust: None,
        }
    }
}

#[derive(Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct ContentTrust {
    pub ca_certs: Option<BTreeMap<String, Url>>,
}
