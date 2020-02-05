// Copyright (c) Microsoft. All rights reserved.

use futures::Future;

use edgelet_core::{Module, ModuleRuntimeState, ModuleTop};

use crate::config::ShellConfig;
use crate::error::Error;

const MODULE_TYPE: &str = "shellrt";

pub struct ShellModule {
    name: String,
    config: ShellConfig,
}

impl std::fmt::Debug for ShellModule {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("ShellModule").finish()
    }
}

pub trait ShellModuleTop {
    type Error;
    type ModuleTopFuture: Future<Item = ModuleTop, Error = Self::Error> + Send;

    fn top(&self) -> Self::ModuleTopFuture;
}

impl ShellModuleTop for ShellModule {
    type Error = Error;
    type ModuleTopFuture = Box<dyn Future<Item = ModuleTop, Error = Self::Error> + Send>;

    fn top(&self) -> Self::ModuleTopFuture {
        // edgelet:
        // - shell out { ? }
        // shellrt-containerd
        // - (containerd) (tasks.proto) ListPids
        unimplemented!()
    }
}

impl Module for ShellModule {
    type Config = ShellConfig;
    type Error = Error;
    type RuntimeStateFuture =
        Box<dyn Future<Item = ModuleRuntimeState, Error = Self::Error> + Send>;

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
        // edgelet:
        // - shell out { ? }
        // shellrt-containerd
        // - (containerd) (tasks.proto) ListPids
        // - extract relevant data from ListPidsResponse
        unimplemented!()
    }
}
