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
use hyper::{Body, Request};

#[derive(Clone, Debug)]
pub struct TestRegistry<C> {
    err: bool,
    phantom: PhantomData<C>,
}

impl<C> TestRegistry<C> {
    pub fn new(err: bool) -> Self {
        TestRegistry {
            err,
            phantom: PhantomData,
        }
    }
}

impl<C> ModuleRegistry for TestRegistry<C>
{
    type PullFuture = FutureResult<(), anyhow::Error>;
    type RemoveFuture = FutureResult<(), anyhow::Error>;
    type Config = C;

    fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
        if self.err {
            return future::err(anyhow::anyhow!("TestRegistry::pull"));
        }
        future::ok(())
    }

    fn remove(&self, _name: &str) -> Self::RemoveFuture {
        if self.err {
            return future::err(anyhow::anyhow!("TestRegistry::remove"));
        }
        future::ok(())
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
pub struct TestModule<C> {
    name: String,
    config: C,
    state: Option<ModuleRuntimeState>,
    logs: TestBody,
}

impl<C> TestModule<C> {
    pub fn new(name: String, config: C, state: Option<ModuleRuntimeState>) -> Self {
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
        state: Option<ModuleRuntimeState>,
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

impl<C> Module for TestModule<C>
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
        if let Some(state) = self.state.clone() {
            future::ok(state)
        }
        else {
            future::err(anyhow::anyhow!("TestModule::runtime_state"))
        }
    }
}

#[derive(Clone)]
pub struct TestRuntime<S>
where
    S: RuntimeSettings,
{
    module: Option<TestModule<S::Config>>,
    registry: TestRegistry<S::Config>,
    settings: S,
}

impl<S> TestRuntime<S>
where
    S: RuntimeSettings,
{
    pub fn with_module(mut self, module: TestModule<S::Config>) -> Self {
        self.module = Some(module);
        self
    }

    pub fn with_registry(mut self, registry: TestRegistry<S::Config>) -> Self {
        self.registry = registry;
        self
    }
}

impl<S> Authenticator for TestRuntime<S>
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

impl<S> MakeModuleRuntime for TestRuntime<S>
where
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
            registry: TestRegistry::new(false),
            settings,
        })
    }
}

impl<S> ModuleRuntime for TestRuntime<S>
where
    S: RuntimeSettings + Send,
    S::Config: Clone + Send + 'static,
{
    type Config = S::Config;
    type Module = TestModule<S::Config>;
    type ModuleRegistry = TestRegistry<S::Config>;
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
        if self.module.is_none() {
            return future::err(anyhow::anyhow!("TestRuntime::create"));
        }
        future::ok(())
    }

    fn get(&self, _id: &str) -> Self::GetFuture {
        if let Some(module) = self.module.clone() {
            future::result(module.runtime_state()
                .poll()
                .map(move |runtime_state| match runtime_state {
                    futures::Async::Ready(runtime_state) => (module, runtime_state),
                    _ => panic!("TestModule::runtime_state should return FutureResult")
                }))
        }
        else {
            future::err(anyhow::anyhow!("TestRuntime::get")) 
        }
    }

    fn start(&self, _id: &str) -> Self::StartFuture {
        if self.module.is_none() {
            return future::err(anyhow::anyhow!("TestRuntime::start"));
        }
        future::ok(())
    }

    fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
        if self.module.is_none() {
            return future::err(anyhow::anyhow!("TestRuntime::stop"));
        }
        future::ok(())
    }

    fn restart(&self, _id: &str) -> Self::RestartFuture {
        if self.module.is_none() {
            return future::err(anyhow::anyhow!("TestRuntime::restart"));
        }
        future::ok(())
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        if self.module.is_none() {
            return future::err(anyhow::anyhow!("TestRuntime::remove"));
        }
        future::ok(())
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        if self.module.is_some() {
            future::ok(SystemInfo {
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
            })
        }
        else {
            future::err(anyhow::anyhow!("TestRuntime::system_info"))
        }
    }

    fn system_resources(&self) -> Self::SystemResourcesFuture {
        if self.module.is_some() {
            future::ok(SystemResources::new(
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
            ))
        }
        else {
            future::err(anyhow::anyhow!("TestRuntime::system_resources"))
        }
    }

    fn list(&self) -> Self::ListFuture {
        if let Some(module) = self.module.clone() {
            future::ok(vec![module])
        }
        else {
            future::err(anyhow::anyhow!("TestRuntime::list"))
        }
    }

    fn list_with_details(&self) -> Self::ListWithDetailsStream {
        if let Some(module) = self.module.clone() {
            Box::new(module
                .runtime_state()
                .map(move |runtime_state| (module, runtime_state))
                .into_stream())
        }
        else {
            Box::new(stream::once(Err(anyhow::anyhow!("TestRuntime::list_with_details"))))
        }
    }

    fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
        if let Some(module) = &self.module {
            future::ok(module.logs.clone())
        }
        else {
            future::err(anyhow::anyhow!("TestRuntime::logs"))
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
