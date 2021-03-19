// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;

use url::Url;

#[derive(Debug, serde::Deserialize, serde::Serialize)]
pub(super) struct Config {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub(super) parent_hostname: Option<String>,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub(super) trust_bundle_cert: Option<Url>,

    #[serde(flatten)]
    pub(super) aziot: aziotctl_common::config::super_config::Config,

    #[serde(default = "default_agent")]
    pub(super) agent: edgelet_core::ModuleSpec<edgelet_docker::DockerConfig>,

    #[serde(default)]
    pub(super) connect: edgelet_core::Connect,
    #[serde(default)]
    pub(super) listen: edgelet_core::Listen,

    #[serde(default)]
    pub(super) watchdog: edgelet_core::WatchdogSettings,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub(super) edge_ca: Option<EdgeCa>,

    #[serde(default)]
    pub(super) moby_runtime: MobyRuntime,
}

pub(super) fn default_agent() -> edgelet_core::ModuleSpec<edgelet_docker::DockerConfig> {
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
pub(super) enum EdgeCa {
    Explicit {
        cert: Url,
        pk: Url,
    },
    Quickstart {
        auto_generated_edge_ca_expiry_days: u32,
    },
}

#[derive(Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub(super) struct MobyRuntime {
    pub(super) uri: Url,
    pub(super) network: edgelet_core::MobyNetwork,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub(super) content_trust: Option<ContentTrust>,
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
