// Copyright (c) Microsoft. All rights reserved.

use std::time::Duration;

use futures::prelude::*;
use futures::Stream;

use edgelet_core::{
    AuthId, Authenticator, GetTrustBundle, LogOptions, MakeModuleRuntime, ModuleRegistry,
    ModuleRuntime, ModuleRuntimeState, ModuleSpec, SystemInfo as CoreSystemInfo,
};
use provisioning::ProvisioningResult;

use crate::config::ShellConfig;
use crate::error::Error;
use crate::module::ShellModule;
use crate::settings::Settings;

#[derive(Clone)]
pub struct ShellModuleRuntime {}

impl std::fmt::Debug for ShellModuleRuntime {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("ShellModuleRuntime").finish()
    }
}

impl ModuleRegistry for ShellModuleRuntime {
    type Error = Error;
    type PullFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type RemoveFuture = Box<dyn Future<Item = (), Error = Self::Error>>;
    type Config = ShellConfig;

    fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
        unimplemented!()
    }

    fn remove(&self, _name: &str) -> Self::RemoveFuture {
        unimplemented!()
    }
}

impl MakeModuleRuntime for ShellModuleRuntime {
    type Config = ShellConfig;
    type Settings = Settings;
    type ProvisioningResult = ProvisioningResult;
    type ModuleRuntime = Self;
    type Error = Error;

    type Future = Box<dyn Future<Item = Self, Error = Self::Error> + Send>;

    fn make_runtime(
        _settings: Settings,
        _: ProvisioningResult,
        _: impl GetTrustBundle,
    ) -> Self::Future {
        unimplemented!()
    }
}

impl ModuleRuntime for ShellModuleRuntime {
    type Error = Error;
    type Config = ShellConfig;
    type Module = ShellModule;
    type ModuleRegistry = Self;
    type Chunk = Chunk;
    type Logs = Logs;

    type CreateFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type GetFuture =
        Box<dyn Future<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type ListFuture = Box<dyn Future<Item = Vec<Self::Module>, Error = Self::Error> + Send>;
    type ListWithDetailsStream =
        Box<dyn Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type LogsFuture = Box<dyn Future<Item = Self::Logs, Error = Self::Error> + Send>;
    type RemoveFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type RestartFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type StartFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type StopFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type SystemInfoFuture = Box<dyn Future<Item = CoreSystemInfo, Error = Self::Error> + Send>;
    type RemoveAllFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;

    fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        unimplemented!()
    }

    fn get(&self, _id: &str) -> Self::GetFuture {
        unimplemented!()
    }

    fn start(&self, _id: &str) -> Self::StartFuture {
        unimplemented!()
    }

    fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
        unimplemented!()
    }

    fn restart(&self, _id: &str) -> Self::RestartFuture {
        unimplemented!()
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        unimplemented!()
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        unimplemented!()
    }

    fn list(&self) -> Self::ListFuture {
        unimplemented!()
    }

    fn list_with_details(&self) -> Self::ListWithDetailsStream {
        unimplemented!()
    }

    fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
        unimplemented!()
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        unimplemented!()
    }

    fn remove_all(&self) -> Self::RemoveAllFuture {
        unimplemented!()
    }
}

impl Authenticator for ShellModuleRuntime {
    type Error = Error;
    type Request = hyper::Request<hyper::Body>;
    type AuthenticateFuture = Box<dyn Future<Item = AuthId, Error = Self::Error> + Send>;

    fn authenticate(&self, _req: &Self::Request) -> Self::AuthenticateFuture {
        unimplemented!()
    }
}

#[derive(Debug)]
pub struct Logs;

impl Stream for Logs {
    type Item = Chunk;
    type Error = Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        unimplemented!()
    }
}

impl From<Logs> for hyper::Body {
    fn from(_logs: Logs) -> Self {
        unimplemented!()
    }
}

#[derive(Debug, Default)]
pub struct Chunk;

impl Extend<u8> for Chunk {
    fn extend<T>(&mut self, _iter: T)
    where
        T: IntoIterator<Item = u8>,
    {
        unimplemented!()
    }
}

impl AsRef<[u8]> for Chunk {
    fn as_ref(&self) -> &[u8] {
        unimplemented!()
    }
}
