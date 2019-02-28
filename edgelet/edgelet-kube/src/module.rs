// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Module, ModuleRuntimeState, ModuleStatus};
use edgelet_docker::DockerConfig;
use edgelet_utils::ensure_not_empty_with_context;
use futures::{future, Future};

use crate::error::{Error, ErrorKind, Result};

const MODULE_TYPE: &str = "docker";

pub struct KubeModule {
    name: String,
    config: DockerConfig,
}

impl KubeModule {
    pub fn new(name: String, config: DockerConfig) -> Result<Self> {
        ensure_not_empty_with_context(&name, || ErrorKind::InvalidModuleName(name.clone()))?;

        Ok(KubeModule { name, config })
    }
}

impl Module for KubeModule {
    type Config = DockerConfig;
    type Error = Error;
    type RuntimeStateFuture = Box<Future<Item = ModuleRuntimeState, Error = Self::Error> + Send>;

    fn name(&self) -> &str {
        &self.name
    }

    fn type_(&self) -> &str {
        MODULE_TYPE
    }

    fn config(&self) -> &Self::Config {
        &self.config
    }

    fn runtime_state(&self) -> Self::RuntimeStateFuture {
        // Working on assumption that if Kube module exists (present in cluster), status is successful
        // TODO: get Pod "last known good state" when we implement a more robust recovery in iotedged
        Box::new(future::ok(
            ModuleRuntimeState::default().with_status(ModuleStatus::Running),
        ))
    }
}
