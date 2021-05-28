// Copyright (c) Microsoft. All rights reserved.

use std::path::Path;
use std::time::Duration;

use core::pin::Pin;

use failure::Fail;
use futures::task::{Context, Poll};
use futures::Stream;
use hyper::{Body, Request};

use edgelet_core::{
    settings::AutoReprovisioningMode, AuthId, Authenticator, Connect, DiskInfo, Endpoints, Listen,
    LogOptions, MakeModuleRuntime, Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState,
    ModuleSpec, ProvisioningInfo, RuntimeSettings, SystemInfo, SystemResources, WatchdogSettings,
};

#[derive(Clone, Debug)]
pub struct TestRegistry<E: Clone + Fail> {
    err: Option<E>,
}

impl<E: Clone + Fail> TestRegistry<E> {
    pub fn new(err: Option<E>) -> Self {
        TestRegistry { err }
    }
}

#[async_trait::async_trait]
impl<E: Clone + Fail> ModuleRegistry for TestRegistry<E> {
    type Error = E;
    type Config = TestConfig;

    async fn pull(&self, _config: &Self::Config) -> Result<(), Self::Error> {
        match &self.err {
            Some(e) => Err(e.clone()),
            None => Ok(()),
        }
    }

    async fn remove(&self, _name: &str) -> Result<(), Self::Error> {
        match &self.err {
            Some(e) => Err(e.clone()),
            None => Ok(()),
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

    fn edge_ca_cert(&self) -> Option<&str> {
        unimplemented!()
    }

    fn edge_ca_key(&self) -> Option<&str> {
        unimplemented!()
    }

    fn trust_bundle_cert(&self) -> Option<&str> {
        unimplemented!()
    }

    fn manifest_trust_bundle_cert(&self) -> Option<&str> {
        unimplemented!()
    }

    fn auto_reprovisioning_mode(&self) -> &AutoReprovisioningMode {
        unimplemented!()
    }
}

#[derive(Clone, Debug)]
pub struct TestBody {
    data: Vec<&'static [u8]>,
}

impl TestBody {
    pub fn new(logs: Vec<&'static [u8]>) -> Self {
        let mut logs = logs;
        logs.reverse();

        TestBody { data: logs }
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

    fn poll_next(self: Pin<&mut Self>, _cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        match self.get_mut().data.pop() {
            Some(d) => Poll::Ready(Some(d)),
            None => Poll::Ready(None),
        }
    }
}

#[derive(Clone, Debug)]
pub struct TestModule<E: Clone + Fail> {
    name: String,
    config: TestConfig,
    state: Result<ModuleRuntimeState, E>,
    logs: TestBody,
}

impl<E: Clone + Fail> TestModule<E> {
    pub fn new(name: String, config: TestConfig, state: Result<ModuleRuntimeState, E>) -> Self {
        TestModule {
            name,
            config,
            state,
            logs: TestBody::default(),
        }
    }

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

#[async_trait::async_trait]
impl<E: Clone + Fail> Module for TestModule<E> {
    type Config = TestConfig;
    type Error = E;

    fn name(&self) -> &str {
        &self.name
    }

    fn type_(&self) -> &str {
        "test"
    }

    fn config(&self) -> &Self::Config {
        &self.config
    }

    async fn runtime_state(&self) -> Result<ModuleRuntimeState, Self::Error> {
        self.state.clone()
    }
}

#[derive(Clone)]
pub struct TestRuntime<E: Clone + Fail> {
    module: Option<Result<TestModule<E>, E>>,
    registry: TestRegistry<E>,
    settings: TestSettings,
}

impl<E: Clone + Fail> TestRuntime<E> {
    pub fn with_module(mut self, module: Result<TestModule<E>, E>) -> Self {
        self.module = Some(module);
        self
    }

    pub fn with_registry(mut self, registry: TestRegistry<E>) -> Self {
        self.registry = registry;
        self
    }
}

#[async_trait::async_trait]
impl<E: Clone + Fail> Authenticator for TestRuntime<E> {
    type Error = E;
    type Request = Request<Body>;

    async fn authenticate(&self, _req: &Self::Request) -> Result<AuthId, Self::Error> {
        Ok(AuthId::Any)
    }
}

#[async_trait::async_trait]
impl<E: Clone + Fail> MakeModuleRuntime for TestRuntime<E> {
    type Config = TestConfig;
    type Settings = TestSettings;
    type ModuleRuntime = Self;
    type Error = E;

    async fn make_runtime(settings: Self::Settings) -> Result<Self::ModuleRuntime, Self::Error> {
        Ok(TestRuntime {
            module: None,
            registry: TestRegistry::new(None),
            settings,
        })
    }
}

#[async_trait::async_trait]
impl<E: Clone + Fail> ModuleRuntime for TestRuntime<E> {
    type Error = E;

    type Config = TestConfig;
    type Module = TestModule<E>;
    type ModuleRegistry = TestRegistry<E>;
    type Chunk = &'static [u8];
    type Logs = TestBody;

    async fn create(&self, _module: ModuleSpec<Self::Config>) -> Result<(), Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(_) => Ok(()),
            Err(e) => Err(e.clone()),
        }
    }

    async fn get(&self, _id: &str) -> Result<(Self::Module, ModuleRuntimeState), Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(m) => Ok((m.clone(), ModuleRuntimeState::default())),
            Err(e) => Err(e.clone()),
        }
    }

    async fn start(&self, _id: &str) -> Result<(), Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(_) => Ok(()),
            Err(e) => Err(e.clone()),
        }
    }

    async fn stop(
        &self,
        _id: &str,
        _wait_before_kill: Option<Duration>,
    ) -> Result<(), Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(_) => Ok(()),
            Err(e) => Err(e.clone()),
        }
    }

    async fn restart(&self, _id: &str) -> Result<(), Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(_) => Ok(()),
            Err(e) => Err(e.clone()),
        }
    }

    async fn remove(&self, _id: &str) -> Result<(), Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(_) => Ok(()),
            Err(e) => Err(e.clone()),
        }
    }

    async fn system_info(&self) -> Result<SystemInfo, Self::Error> {
        /*match self.module.as_ref().unwrap() {
            Ok(_) => Ok(SystemInfo {
                os_type: "os_type_sample".to_string(),
                architecture: "architecture_sample".to_string(),
                version: edgelet_core::version_with_source_version(),
                provisioning: ProvisioningInfo {
                    r#type: "test".to_string(),
                    dynamic_reprovisioning: false,
                    always_reprovision_on_startup: true,
                },
                cpus: 0,
                virtualized: "test".to_string(),
                kernel_version: "test".to_string(),
                operating_system: "test".to_string(),
                server_version: "test".to_string(),
            }),
            Err(e) => Err(e.clone()),
        }*/

        Ok(SystemInfo {
            os_type: "os_type_sample".to_string(),
            architecture: "architecture_sample".to_string(),
            version: edgelet_core::version_with_source_version(),
            provisioning: ProvisioningInfo {
                r#type: "test".to_string(),
                dynamic_reprovisioning: false,
                always_reprovision_on_startup: true,
            },
            cpus: 0,
            virtualized: "test".to_string(),
            kernel_version: "test".to_string(),
            operating_system: "test".to_string(),
            server_version: "test".to_string(),
        })
    }

    async fn system_resources(&self) -> Result<SystemResources, Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(_) => Ok(SystemResources::new(
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
            Err(e) => Err(e.clone()),
        }
    }

    async fn list(&self) -> Result<Vec<Self::Module>, Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(m) => Ok(vec![m.clone()]),
            Err(e) => Err(e.clone()),
        }
    }

    async fn list_with_details(&self) -> Result<(Self::Module, ModuleRuntimeState), Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(m) => {
                let state = m.runtime_state().await?;
                Ok((m.clone(), state))
            }
            Err(e) => Err(e.clone()),
        }
    }

    async fn logs(&self, _id: &str, _options: &LogOptions) -> Result<Self::Logs, Self::Error> {
        match self.module.as_ref().unwrap() {
            Ok(m) => Ok(m.logs.clone()),
            Err(e) => Err(e.clone()),
        }
    }

    async fn remove_all(&self) -> Result<(), Self::Error> {
        Ok(())
    }

    async fn stop_all(&self, _wait_before_kill: Option<Duration>) -> Result<(), Self::Error> {
        Ok(())
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        &self.registry
    }
}
