// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Module, ModuleRuntimeState};
use edgelet_docker::DockerConfig;
use edgelet_utils::ensure_not_empty_with_context;
use futures::{future, Future};
use kube_client::{Client as KubeClient, ValueToken};

use error::{Error, ErrorKind, Result};

const MODULE_TYPE: &str = "docker";

pub struct KubeModule {
    _client: KubeClient<ValueToken>,
    name: String,
    config: DockerConfig,
}

impl KubeModule {
    pub fn new(client: KubeClient<ValueToken>, name: String, config: DockerConfig) -> Result<Self> {
        ensure_not_empty_with_context(&name, || ErrorKind::InvalidModuleName(name.clone()))?;

        Ok(KubeModule {
            _client: client,
            name,
            config,
        })
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
        Box::new(future::err(Error::from(ErrorKind::Kubernetes)))
    }
}
