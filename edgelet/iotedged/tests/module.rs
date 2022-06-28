use edgelet_core::ModuleAction;
use edgelet_core::{
    AuthId, Authenticator, Certificates, Connect, GetTrustBundle, Listen, LogOptions,
    MakeModuleRuntime, Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, ModuleSpec,
    Provisioning, ProvisioningResult, RuntimeSettings, SystemInfo, SystemResources,
    WatchdogSettings,
};
use edgelet_docker::{Error, ErrorKind};
use futures::future::{self, FutureResult};
use futures::prelude::*;
use futures::stream;
use futures::sync::mpsc::UnboundedSender;
use hyper::{Body, Request};
use std::marker::PhantomData;
use std::path::{Path, PathBuf};
use std::time::Duration;

#[derive(Clone)]
pub struct TestRegistry<TestConfig> {
    phantom: PhantomData<TestConfig>,
}

impl<TestConfig> TestRegistry<TestConfig> {
    pub fn new() -> Self {
        TestRegistry {
            phantom: PhantomData,
        }
    }
}

impl Default for TestRegistry<TestConfig> {
    fn default() -> Self {
        Self::new()
    }
}

impl ModuleRegistry for TestRegistry<TestConfig> {
    type Error = Error;
    type PullFuture = FutureResult<(), Self::Error>;
    type RemoveFuture = FutureResult<(), Self::Error>;
    type Config = TestConfig;

    fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
        unimplemented!()
    }

    fn remove(&self, _name: &str) -> Self::RemoveFuture {
        unimplemented!()
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

#[derive(Clone, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct TestSettings {
    listen: Listen,
    homedir: PathBuf,
}

impl TestSettings {
    pub fn new() -> Self {
        unimplemented!()
    }
}

impl Default for TestSettings {
    fn default() -> Self {
        Self::new()
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

    fn allow_elevated_docker_permissions(&self) -> bool {
        unimplemented!()
    }

    fn connect(&self) -> &Connect {
        unimplemented!()
    }

    fn listen(&self) -> &Listen {
        &self.listen
    }

    fn homedir(&self) -> &Path {
        &self.homedir
    }

    fn certificates(&self) -> &Certificates {
        unimplemented!()
    }

    fn watchdog(&self) -> &WatchdogSettings {
        unimplemented!()
    }
}

#[derive(Clone, Debug)]
pub struct TestMod {
    name: String,
    config: TestConfig,
    logs: TestBody,
}

impl TestMod {
    pub fn new(name: String, config: TestConfig) -> Self {
        TestMod {
            name,
            config,
            logs: TestBody::default(),
        }
    }
}

impl Module for TestMod {
    type Config = TestConfig;
    type Error = Error;
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
        unimplemented!()
    }
}

impl TestRuntime {
    pub fn with_module(mut self, module: TestMod) -> Self {
        self.module = Some(module);
        self
    }

    pub fn with_registry(mut self, registry: TestRegistry<TestConfig>) -> Self {
        self.registry = registry;
        self
    }
}

impl Authenticator for TestRuntime {
    type Error = Error;
    type Request = Request<Body>;
    type AuthenticateFuture = Box<dyn Future<Item = AuthId, Error = Self::Error> + Send>;

    fn authenticate(&self, _req: &Self::Request) -> Self::AuthenticateFuture {
        Box::new(future::ok(AuthId::Any))
    }
}

#[derive(Debug)]
pub struct TestBody {
    data: Vec<&'static [u8]>,
    stream: futures::stream::IterOk<std::vec::IntoIter<&'static [u8]>, Error>,
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
    type Error = Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        unimplemented!()
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

#[derive(Default, Clone)]
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
#[derive(Clone)]
pub struct TestRuntime {
    module: Option<TestMod>,
    registry: TestRegistry<TestConfig>,
    settings: TestSettings,
    pub create_socket_channel: UnboundedSender<ModuleAction>,
}
impl MakeModuleRuntime for TestRuntime {
    type Config = TestConfig;
    type Settings = TestSettings;
    type ProvisioningResult = TestProvisioningResult;
    type ModuleRuntime = Self;
    type Error = Error;
    type Future = FutureResult<Self, Error>;

    fn make_runtime(
        settings: Self::Settings,
        _: Self::ProvisioningResult,
        _: impl GetTrustBundle,
        create_socket_channel: UnboundedSender<ModuleAction>,
    ) -> Self::Future {
        future::ok(TestRuntime {
            module: None,
            registry: TestRegistry::new(),
            settings,
            create_socket_channel,
        })
    }
}

impl ModuleRuntime for TestRuntime {
    type Error = Error;
    type Config = TestConfig;
    type Module = TestMod;
    type ModuleRegistry = TestRegistry<TestConfig>;
    type Chunk = &'static [u8];
    type Logs = TestBody;

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
    type StopAllFuture = FutureResult<(), Self::Error>;

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

    fn system_resources(&self) -> Self::SystemResourcesFuture {
        unimplemented!()
    }

    fn list(&self) -> Self::ListFuture {
        match self.module.as_ref() {
            Some(m) => future::ok(vec![m.clone()]),
            None => future::err(Error::from(ErrorKind::Conflict)),
        }
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

    fn stop_all(&self, _wait_before_kill: Option<Duration>) -> Self::StopAllFuture {
        unimplemented!()
    }
}
