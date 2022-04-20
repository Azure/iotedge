use std::marker::PhantomData;
use std::path::Path;
use std::time::Duration;

use edgelet_core::ModuleAction;
use edgelet_core::{
    settings::AutoReprovisioningMode, AuthId, Authenticator, Connect, DiskInfo, Endpoints, Listen,
    LogOptions, MakeModuleRuntime, Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState,
    ModuleSpec, ProvisioningInfo, RuntimeSettings, SystemInfo, SystemResources, WatchdogSettings,
};
use futures::future::{self, FutureResult};
use futures::prelude::*;
use futures::stream;
use futures::sync::mpsc::UnboundedSender;
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
    E: std::error::Error + Clone + Send + Sync + 'static,
{
    type PullFuture = FutureResult<(), anyhow::Error>;
    type RemoveFuture = FutureResult<(), anyhow::Error>;
    type Config = C;

    fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
        match self.err {
            Some(ref e) => future::err(e.clone().into()),
            None => future::ok(()),
        }
    }

    fn remove(&self, _name: &str) -> Self::RemoveFuture {
        match self.err {
            Some(ref e) => future::err(e.clone().into()),
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

    fn watchdog(&self) -> &WatchdogSettings {
        unimplemented!()
    }

    fn endpoints(&self) -> &Endpoints {
        unimplemented!()
    }

    fn additional_info(&self) -> &std::collections::BTreeMap<String, String> {
        unimplemented!()
    }

    fn edge_ca_cert(&self) -> Option<&str> {
        unimplemented!()
    }

    fn edge_ca_key(&self) -> Option<&str> {
        unimplemented!()
    }

    fn trust_bundle_cert(&self) -> Option<&str> {
        unimplemented!()
    }

    fn auto_reprovisioning_mode(&self) -> &AutoReprovisioningMode {
        unimplemented!()
    }
}

#[derive(Clone, Debug)]
pub struct TestModule<E, C> {
    name: String,
    config: C,
    state: Result<ModuleRuntimeState, E>,
    logs: TestBody,
}

impl<E, C> TestModule<E, C> {
    pub fn new(name: String, config: C, state: Result<ModuleRuntimeState, E>) -> Self {
        TestModule {
            name,
            config,
            state,
            logs: TestBody::default(),
        }
    }

    pub fn new_with_logs(
        name: String,
        config: C,
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

impl<E, C> Module for TestModule<E, C>
where
    E: std::error::Error + Clone + Send + Sync + 'static,
{
    type Config = C;
    type RuntimeStateFuture = FutureResult<ModuleRuntimeState, anyhow::Error>;

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
        self.state.clone().map_err(Into::into).into_future()
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
    E: std::error::Error + Clone + Send + Sync + 'static,
    S: RuntimeSettings,
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
{
    type Request = Request<Body>;
    type AuthenticateFuture = Box<dyn Future<Item = AuthId, Error = anyhow::Error> + Send>;

    fn authenticate(&self, _req: &Self::Request) -> Self::AuthenticateFuture {
        Box::new(future::ok(AuthId::Any))
    }
}

#[derive(Debug)]
pub struct TestBody {
    data: Vec<&'static [u8]>,
    stream: futures::stream::IterOk<std::vec::IntoIter<&'static [u8]>, anyhow::Error>,
}

impl TestBody {
    pub fn new(logs: Vec<&'static [u8]>) -> Self {
        TestBody {
            data: logs.clone(),
            stream: stream::iter_ok(logs),
        }
    }
}

impl Default for TestBody {
    fn default() -> Self {
        TestBody::new(vec![&[
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, b'A',
        ]])
    }
}

impl Stream for TestBody {
    type Item = &'static [u8];
    type Error = anyhow::Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        self.stream.poll()
    }
}

impl Clone for TestBody {
    fn clone(&self) -> Self {
        Self::new(self.data.clone())
    }
}

impl From<TestBody> for Body {
    fn from(old: TestBody) -> Body {
        let temp: Vec<u8> = old.data.into_iter().flat_map(ToOwned::to_owned).collect();
        Body::from(temp)
    }
}

impl<E, S> MakeModuleRuntime for TestRuntime<E, S>
where
    E: std::error::Error + Clone + Send + Sync + 'static,
    S: RuntimeSettings + Send,
    S::Config: Clone + Send + 'static,
{
    type Config = S::Config;
    type Settings = S;
    type ModuleRuntime = Self;
    type Future = FutureResult<Self, anyhow::Error>;

    fn make_runtime(
        settings: Self::Settings,
        _create_socket_channel: UnboundedSender<ModuleAction>,
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
    E: std::error::Error + Clone + Send + Sync + 'static,
    S: RuntimeSettings + Send,
    S::Config: Clone + Send + 'static,
{
    type Config = S::Config;
    type Module = TestModule<E, S::Config>;
    type ModuleRegistry = TestRegistry<E, S::Config>;
    type Chunk = &'static [u8];
    type Logs = TestBody;

    type CreateFuture = FutureResult<(), anyhow::Error>;
    type GetFuture = FutureResult<(Self::Module, ModuleRuntimeState), anyhow::Error>;
    type ListFuture = FutureResult<Vec<Self::Module>, anyhow::Error>;
    type ListWithDetailsStream =
        Box<dyn Stream<Item = (Self::Module, ModuleRuntimeState), Error = anyhow::Error> + Send>;
    type LogsFuture = FutureResult<Self::Logs, anyhow::Error>;
    type RemoveFuture = FutureResult<(), anyhow::Error>;
    type RestartFuture = FutureResult<(), anyhow::Error>;
    type StartFuture = FutureResult<(), anyhow::Error>;
    type StopFuture = FutureResult<(), anyhow::Error>;
    type SystemInfoFuture = FutureResult<SystemInfo, anyhow::Error>;
    type SystemResourcesFuture = FutureResult<SystemResources, anyhow::Error>;
    type RemoveAllFuture = FutureResult<(), anyhow::Error>;
    type StopAllFuture = FutureResult<(), anyhow::Error>;

    fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone().into()),
        }
    }

    fn get(&self, _id: &str) -> Self::GetFuture {
        match self.module.as_ref().unwrap() {
            Ok(ref m) => future::ok((m.clone(), ModuleRuntimeState::default())),
            Err(ref e) => future::err(e.clone().into()),
        }
    }

    fn start(&self, _id: &str) -> Self::StartFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone().into()),
        }
    }

    fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone().into()),
        }
    }

    fn restart(&self, _id: &str) -> Self::RestartFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone().into()),
        }
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone().into()),
        }
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        match self.module.as_ref().unwrap() {
            Ok(_) => future::ok(SystemInfo {
                kernel: "linux".to_owned(),
                kernel_release: "5.0".to_owned(),
                kernel_version: "1".to_owned(),

                operating_system: "os".to_owned().into(),
                operating_system_version: "version".to_owned().into(),
                operating_system_variant: "variant".to_owned().into(),
                operating_system_build: "1".to_owned().into(),

                architecture: "architecture_sample".to_owned(),
                cpus: 0,
                virtualized: "test".to_owned(),

                product_name: "product".to_owned().into(),
                system_vendor: "vendor".to_owned().into(),

                version: edgelet_core::version_with_source_version().to_owned(),
                provisioning: ProvisioningInfo {
                    r#type: "test".to_string(),
                    dynamic_reprovisioning: false,
                    always_reprovision_on_startup: true,
                },

                additional_properties: std::collections::BTreeMap::new(),
            }),
            Err(ref e) => future::err(e.clone().into()),
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
            Err(ref e) => future::err(e.clone().into()),
        }
    }

    fn list(&self) -> Self::ListFuture {
        match self.module.as_ref().unwrap() {
            Ok(ref m) => future::ok(vec![m.clone()]),
            Err(ref e) => future::err(e.clone().into()),
        }
    }

    fn list_with_details(&self) -> Self::ListWithDetailsStream {
        match self.module.as_ref().unwrap() {
            Ok(ref m) => {
                let m = m.clone();
                Box::new(m.runtime_state().map(|rs| (m, rs)).into_stream())
            }
            Err(ref e) => Box::new(stream::once(Err(e.clone().into()))),
        }
    }

    fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
        match self.module.as_ref().unwrap() {
            Ok(ref m) => future::ok(m.logs.clone()),
            Err(ref e) => future::err(e.clone().into()),
        }
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        &self.registry
    }

    fn remove_all(&self) -> Self::RemoveAllFuture {
        future::ok(())
    }

    fn stop_all(&self, _wait_before_kill: Option<Duration>) -> Self::StopAllFuture {
        future::ok(())
    }
}
