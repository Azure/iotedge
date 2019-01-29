// Copyright (c) Microsoft. All rights reserved.

use crate::error::Error;

use edgelet_core::ModuleSpec;
use edgelet_docker::DockerConfig;
use k8s_openapi::v1_10::api::apps::v1 as apps;

pub fn spec_to_deployment(_spec: &ModuleSpec<DockerConfig>) -> Result<apps::Deployment, Error> {
    unimplemented!()
}
