// Copyright (c) Microsoft. All rights reserved.

use std::marker::PhantomData;
use std::path::Path;
use std::time::Duration;

use edgelet_core::*;
use failure::Fail;
use futures::future::{self, FutureResult};
use futures::prelude::*;
use futures::stream;
use futures::IntoFuture;
use hyper::{Body, Request};

#[derive(Clone, Debug)]
pub struct TestRegistry<E, C> {
    err: Option<E>,
    phantom: PhantomData<C>,
}

impl<E, C> TestRegistry<E, C> {
    pub fn new(err: Option<E>) -> Self {
        TestRegistry {
            err,
            phantom: PhantomData,
        }
    }
}

impl<E, C> ModuleRegistry for TestRegistry<E, C>
where
    E: Clone + Fail + Send + Sync,
{
    type Error = E;
    type PullFuture = FutureResult<(), Self::Error>;
    type RemoveFuture = FutureResult<(), Self::Error>;
    type Config = C;

    fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
        match self.err {
            Some(ref e) => future::err(e.clone()),
            None => future::ok(()),
        }
    }

    fn remove(&self, _name: &str) -> Self::RemoveFuture {
        match self.err {
            Some(ref e) => future::err(e.clone()),
            None => future::ok(()),
        }
    }
}

#[derive(Clone, Debug, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct TestConfig {
    image: String,
}

impl TestConfig {
    pub fn new(image: String) -> Self {
        TestConfig { image }
    }

    pub fn image(&self) -> &str {
        &self.image
    }
}

#[derive(Clone, Default, serde_derive::Serialize)]
pub struct TestSettings;

impl TestSettings {
    pub fn new() -> Self {
        TestSettings {}
    }
}

impl RuntimeSettings for TestSettings {
    type Config = TestConfig;

    fn provisioning(&self) -> &Provisioning {
        unimplemented!()
    }

    fn agent(&self) -> &ModuleSpec<Self::Config> {
        unimplemented!()
    }

    fn agent_mut(&mut self) -> &mut ModuleSpec<Self::Config> {
        unimplemented!()
    }

    fn hostname(&self) -> &str {
        unimplemented!()
    }

    fn connect(&self) -> &Connect {
        unimplemented!()
    }

    fn listen(&self) -> &Listen {
        unimplemented!()
    }

    fn homedir(&self) -> &Path {
        unimplemented!()
    }

    fn certificates(&self) -> &Certificates {
        unimplemented!()
    }

    fn watchdog(&self) -> &WatchdogSettings {
        unimplemented!()
    }
}

#[derive(Clone, Debug)]
pub struct TestModule<E, C> {
    name: String,
    config: C,
    state: Result<ModuleRuntimeState, E>,
    logs: TestBody<E>,
}

impl<E: Fail> TestModule<E, TestConfig> {
    pub fn new(name: String, config: TestConfig, state: Result<ModuleRuntimeState, E>) -> Self {
        TestModule {
            name,
            config,
            state,
            logs: TestBody::default(),
        }
    }
}

impl<E: Fail, C> TestModule<E, C> {
    pub fn new_with_config(name: String, config: C, state: Result<ModuleRuntimeState, E>) -> Self {
        TestModule {
            name,
            config,
            state,
            logs: TestBody::default(),
        }
    }
}

impl<E: Fail> TestModule<E, TestConfig> {
    pub fn new_with_logs(
        name: String,
        config: TestConfig,
        state: Result<ModuleRuntimeState, E>,
        logs: Vec<&'static [u8]>,
    ) -> Self {
        TestModule {
            name,
            config,
            state,
            logs: TestBody::new(logs),
        }
    }
}

impl<E: Clone + Fail, C> Module for TestModule<E, C> {
    type Config = C;
    type Error = E;
    type RuntimeStateFuture = FutureResult<ModuleRuntimeState, Self::Error>;

    fn name(&self) -> &str {
        &self.name
    }

    fn type_(&self) -> &str {
        "test"
    }

    fn config(&self) -> &Self::Config {
        &self.config
    }

    fn runtime_state(&self) -> Self::RuntimeStateFuture {
        self.state.clone().into_future()
    }
}

#[derive(Clone)]
pub struct TestRuntime<E, S>
where
    S: RuntimeSettings,
{
    module: Option<Result<TestModule<E, S::Config>, E>>,
    registry: TestRegistry<E, S::Config>,
    settings: S,
}

impl<E, S> TestRuntime<E, S>
where
    S: RuntimeSettings,
    E: Clone + Fail,
{
    pub fn with_module(mut self, module: Result<TestModule<E, S::Config>, E>) -> Self {
        self.module = Some(module);
        self
    }

    pub fn with_registry(mut self, registry: TestRegistry<E, S::Config>) -> Self {
        self.registry = registry;
        self
    }
}

impl<E, S> Authenticator for TestRuntime<E, S>
where
    S: RuntimeSettings,
    E: Clone + Fail,
{
    type Error = E;
    type Request = Request<Body>;
    type AuthenticateFuture = Box<dyn Future<Item = AuthId, Error = Self::Error> + Send>;

    fn authenticate(&self, _req: &Self::Request) -> Self::AuthenticateFuture {
        Box::new(future::ok(AuthId::Any))
    }
}

#[derive(Debug)]
pub struct TestBody<E> {
    data: Vec<&'static [u8]>,
    stream: futures::stream::IterOk<std::vec::IntoIter<&'static [u8]>, E>,
}

impl<E> TestBody<E> {
    pub fn new(logs: Vec<&'static [u8]>) -> Self {
        TestBody {
            data: logs.clone(),
            stream: stream::iter_ok(logs),
        }
    }
}

impl<E> Default for TestBody<E> {
    fn default() -> Self {
        TestBody::new(vec![&[
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, b'A',
        ]])
    }
}

impl<E> Stream for TestBody<E> {
    type Item = &'static [u8];
    type Error = E;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        self.stream.poll()
    }
}

impl<E> Clone for TestBody<E> {
    fn clone(&self) -> Self {
        Self::new(self.data.clone())
    }
}

impl<E> From<TestBody<E>> for Body {
    fn from(old: TestBody<E>) -> Body {
        let temp: Vec<u8> = old.data.into_iter().flat_map(ToOwned::to_owned).collect();
        Body::from(temp)
    }
}

#[derive(Default)]
pub struct TestProvisioningResult;

impl TestProvisioningResult {
    pub fn new() -> Self {
        TestProvisioningResult {}
    }
}

impl ProvisioningResult for TestProvisioningResult {
    fn device_id(&self) -> &str {
        unimplemented!()
    }

    fn hub_name(&self) -> &str {
        unimplemented!()
    }
}

impl<E, S> MakeModuleRuntime for TestRuntime<E, S>
where
    E: Clone + Fail,
    S: RuntimeSettings + Send,
    S::Config: Clone + Send + 'static,
{
    type Config = S::Config;
    type Settings = S;
    type ProvisioningResult = TestProvisioningResult;
    type ModuleRuntime = Self;
    type Error = E;
    type Future = FutureResult<Self, Self::Error>;

    fn make_runtime(
        settings: Self::Settings,
        _: Self::ProvisioningResult,
        _: impl GetTrustBundle,
    ) -> Self::Future {
        future::ok(TestRuntime {
            module: None,
            registry: TestRegistry::new(None),
            settings,
        })
    }
}

impl<E, S> ModuleRuntime for TestRuntime<E, S>
where
    E: Clone + Fail,
    S: RuntimeSettings + Send,
    S::Config: Clone + Send + 'static,
{
    type Error = E;
    type Config = S::Config;
    type Module = TestModule<E, S::Config>;
    type ModuleRegistry = TestRegistry<E, S::Config>;
    type Chunk = &'static [u8];
    type Logs = TestBody<E>;

    type CreateFuture = FutureResult<(), Self::Error>;
    type GetFuture = FutureResult<(Self::Module, ModuleRuntimeState), Self::Error>;
    type ListFuture = FutureResult<Vec<Self::Module>, Self::Error>;
    type ListWithDetailsStream =
        Box<dyn Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type LogsFuture = FutureResult<Self::Logs, Self::Error>;
    type RemoveFuture = FutureResult<(), Self::Error>;
    type RestartFuture = FutureResult<(), Self::Error>;
    type StartFuture = FutureResult<(), Self::Error>;
    type StopFuture = FutureResult<(), Self::Error>;
    type SystemInfoFuture = FutureResult<SystemInfo, Self::Error>;
    type SystemResourcesFuture = FutureResult<SystemResources, Self::Error>;
    type RemoveAllFuture = FutureResult<(), Self::Error>;

    fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn get(&self, _id: &str) -> Self::GetFuture {
        match self.module.as_ref().unwrap() {
            Ok(ref m) => future::ok((m.clone(), ModuleRuntimeState::default())),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn start(&self, _id: &str) -> Self::StartFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn restart(&self, _id: &str) -> Self::RestartFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(SystemInfo::new(
                "os_type_sample".to_string(),
                "architecture_sample".to_string(),
            )),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn system_resources(&self) -> Self::SystemResourcesFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(SystemResources::new(
                595_023,
                200,
                0.25,
                5000,
                8000,
                vec![DiskInfo::new(
                    "test disk".to_owned(),
                    10000,
                    20000,
                    "test system".to_owned(),
                    "test type".to_owned(),
                )],
                "fake docker stats".to_owned(),
            )),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn list(&self) -> Self::ListFuture {
        match self.module.as_ref().unwrap() {
            Ok(ref m) => future::ok(vec![m.clone()]),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn list_with_details(&self) -> Self::ListWithDetailsStream {
        match self.module.as_ref().unwrap() {
            Ok(ref m) => {
                let m = m.clone();
                Box::new(m.runtime_state().map(|rs| (m, rs)).into_stream())
            }
            Err(ref e) => Box::new(stream::once(Err(e.clone()))),
        }
    }

    fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
        match self.module.as_ref().unwrap() {
            Ok(ref m) => future::ok(m.logs.clone()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        &self.registry
    }

    fn remove_all(&self) -> Self::RemoveAllFuture {
        future::ok(())
    }
}
