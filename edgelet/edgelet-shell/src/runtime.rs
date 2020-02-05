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
        // edgelet:
        // - shell out { auth, image }
        // shellrt-containerd
        // - (containrs) fetch manifest
        // - for each blob
        //   - (containerd - content.proto) check if blob exists
        //   - (containrs) download blob
        //   - (containerd) "feed" blob to containerd content store
        //     - Option 1: (content.proto + image.proto) via grpc
        //     - Option 2: some hacky intrusive way (to avoid copies)
        unimplemented!()
    }

    fn remove(&self, _image: &str) -> Self::RemoveFuture {
        // edgelet:
        // - shell out { image }
        // shellrt-containerd
        // - (containrs - images.proto) DeleteImageRequest
        //   - containerd handles pruning automatically
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
        // edgelet:
        // - shell out { network? }
        // shellrt-containerd
        // - Create a new network config if none exists (probably via CNI?)
        // - Create stateful config/metadata files if none exist (structure TBD)
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
        // edgelet:
        // - shell out { module }
        // shellrt-containerd
        // - parse relevant data from module
        // - (containerd) (tasks.proto) Create
        unimplemented!()
    }

    // unlike docker, which only requires a single id string, containerd
    // requires both a container_id and an exec_id.
    // I guess it supports a more granular execution model?
    //
    // To keep things simple, edgelet will shell out with an id, and the external
    // process will have to resolve it to something containerd understands.

    fn get(&self, _id: &str) -> Self::GetFuture {
        // edgelet:
        // - shell out { id }
        // shellrt-containerd
        // - (containerd) (tasks.proto) Get
        // - transform GetResponse into (Self::Module, ModuleRuntimeState)
        unimplemented!()
    }

    fn start(&self, _id: &str) -> Self::StartFuture {
        // edgelet:
        // - shell out { id }
        // shellrt-containerd
        // - (containerd) (tasks.proto) Start
        unimplemented!()
    }

    fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
        // edgelet:
        // - shell out { id, wait_before_kill }
        // shellrt-containerd
        // - (containerd) (tasks.proto) KillRequest
        // - manually handle the timeout (not handled by containerd directly)
        unimplemented!()
    }

    fn restart(&self, _id: &str) -> Self::RestartFuture {
        // edgelet:
        // - shell out { id }
        // shellrt-containerd
        // - manually handle restarting (no friendly containerd API to restart)
        unimplemented!()
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        // edgelet:
        // - shell out { id }
        // shellrt-containerd
        // - (containerd) (tasks.proto) DeleteTaskRequest
        unimplemented!()
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        // edgelet:
        // - shell out { id }
        // shellrt-containerd
        // - manually collect data somehow (no containerd api for system info)
        unimplemented!()
    }

    fn list(&self) -> Self::ListFuture {
        unimplemented!()
    }

    fn list_with_details(&self) -> Self::ListWithDetailsStream {
        // edgelet:
        // - implemented locally by calling `ModuleRuntime::list`, then
        //   `Module::runtime_state` on each Module.
        // implementation will likely be copied from edgelet-docker directly
        unimplemented!()
    }

    fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
        // idk, this will be a bit funky
        // containerd does things a bit differently from docker
        // see https://github.com/projectatomic/containerd/blob/master/docs/attach.md
        unimplemented!()
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        self
    }

    fn remove_all(&self) -> Self::RemoveAllFuture {
        // edgelet:
        // - shell out { id }
        // shellrt-containerd
        // - for each running id:
        //   - (containerd) (tasks.proto) DeleteTaskRequest
        unimplemented!()
    }
}

impl Authenticator for ShellModuleRuntime {
    type Error = Error;
    type Request = hyper::Request<hyper::Body>;
    type AuthenticateFuture = Box<dyn Future<Item = AuthId, Error = Self::Error> + Send>;

    fn authenticate(&self, _req: &Self::Request) -> Self::AuthenticateFuture {
        // Something similar (if not totally identical to) edgelet-docker
        // see module.rs/ShellModuleTop for more
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
