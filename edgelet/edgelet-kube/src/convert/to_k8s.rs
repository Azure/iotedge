// Copyright (c) Microsoft. All rights reserved.

use crate::error::Error;

use edgelet_core::ModuleSpec;
use edgelet_docker::DockerConfig;
use k8s_openapi::v1_10::api::apps::v1 as apps;
// use k8s_openapi::v1_10::api::core::v1 as api_core;
// use k8s_openapi::v1_10::apimachinery::pkg::apis::meta::v1 as api_meta;
// use std::collections::BTreeMap;

pub fn spec_to_deployment(_spec: &ModuleSpec<DockerConfig>) -> Result<apps::Deployment, Error> {
    let deployment = apps::Deployment::default();
    Ok(deployment)
}
