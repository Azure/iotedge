// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;

use docker::models::{AuthConfig, ContainerCreateBody};

#[derive(Debug, serde::Deserialize)]
pub(crate) struct ModuleSpec {
    pub(crate) name: String,

    #[serde(rename = "type")]
    pub(crate) type_: String,

    pub(crate) config: DockerConfig,

    #[serde(default)]
    pub(crate) env: BTreeMap<String, String>,

    #[serde(default)]
    #[serde(rename = "imagePullPolicy")]
    pub(crate) image_pull_policy: ImagePullPolicy,
}

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct DockerConfig {
    pub(crate) image: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    #[serde(rename = "imageHash")]
    pub(crate) image_id: Option<String>,

    #[serde(default = "ContainerCreateBody::new")]
    pub(crate) create_options: ContainerCreateBody,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub(crate) auth: Option<AuthConfig>,
}

#[derive(Debug, serde::Deserialize)]
#[serde(rename_all = "lowercase")]
pub(crate) enum ImagePullPolicy {
    #[serde(rename = "on-create")]
    OnCreate,
    Never,
}

impl Default for ImagePullPolicy {
    fn default() -> Self {
        ImagePullPolicy::OnCreate
    }
}
