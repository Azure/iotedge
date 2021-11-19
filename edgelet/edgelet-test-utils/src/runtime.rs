use std::marker::PhantomData;

// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Default, serde::Deserialize, serde::Serialize)]
pub struct Config {}

#[derive(Clone)]
pub struct TestModule<C> {
    pub name: String,
    pub type_: String,
    pub config: C,
}
pub type Module = TestModule<Config>;

impl<C> Default for TestModule<C>
where
    C: Default,
{
    fn default() -> Self {
        TestModule {
            name: "testModule".to_string(),
            type_: "test".to_string(),
            config: C::default(),
        }
    }
}

#[async_trait::async_trait]
impl<C> edgelet_core::Module for TestModule<C>
where
    C: Send + Sync,
{
    type Config = C;
    type Error = std::io::Error;

    fn name(&self) -> &str {
        &self.name
    }

    fn type_(&self) -> &str {
        &self.type_
    }

    fn config(&self) -> &Self::Config {
        &self.config
    }

    // The functions below aren't used in tests.

    async fn runtime_state(&self) -> Result<edgelet_core::ModuleRuntimeState, Self::Error> {
        unimplemented!()
    }
}

#[derive(Default)]
pub struct TestModuleRegistry<C> {
    phantom: PhantomData<C>,
}
pub type ModuleRegistry = TestModuleRegistry<Config>;

#[async_trait::async_trait]
impl<C> edgelet_core::ModuleRegistry for TestModuleRegistry<C>
where
    C: Send + Sync,
{
    type Config = C;
    type Error = std::io::Error;

    // The fuctions below aren't used in tests.

    async fn pull(&self, _config: &Self::Config) -> Result<(), Self::Error> {
        unimplemented!()
    }

    async fn remove(&self, _name: &str) -> Result<(), Self::Error> {
        unimplemented!()
    }
}

pub struct TestRuntime<C> {
    pub module_auth: std::collections::BTreeMap<String, Vec<i32>>,
    pub module_details: Vec<(TestModule<C>, edgelet_core::ModuleRuntimeState)>,
}
pub type Runtime = TestRuntime<Config>;

impl<C> Default for TestRuntime<C> {
    fn default() -> Self {
        // The PID in module_top is used for auth. Bypass auth when testing by always placing
        // this process's PID in the default module_top response.
        let pid = nix::unistd::getpid().as_raw();

        let mut modules = std::collections::BTreeMap::new();
        modules.insert("default".to_string(), vec![pid]);

        TestRuntime {
            module_auth: modules,
            module_details: Vec::new(),
        }
    }
}

#[async_trait::async_trait]
impl<C> edgelet_core::ModuleRuntime for TestRuntime<C>
where
    C: serde::Serialize + Send + Sync + Clone,
{
    type Error = std::io::Error;

    type Config = C;
    type Module = TestModule<C>;
    type ModuleRegistry = TestModuleRegistry<C>;

    async fn module_top(&self, id: &str) -> Result<Vec<i32>, Self::Error> {
        if id == "runtimeError" {
            Err(crate::test_error())
        } else {
            let pids = if let Some(pids) = self.module_auth.get(id) {
                pids.clone()
            } else if let Some(default) = self.module_auth.get("default") {
                default.clone()
            } else {
                Vec::new()
            };

            Ok(pids)
        }
    }

    async fn list_with_details(
        &self,
    ) -> Result<Vec<(Self::Module, edgelet_core::ModuleRuntimeState)>, Self::Error> {
        Ok(self.module_details.clone())
    }

    // The functions below aren't used in tests.

    async fn create(
        &self,
        _module: edgelet_settings::ModuleSpec<Self::Config>,
    ) -> Result<(), Self::Error> {
        unimplemented!()
    }

    async fn get(
        &self,
        _id: &str,
    ) -> Result<(Self::Module, edgelet_core::ModuleRuntimeState), Self::Error> {
        unimplemented!()
    }

    async fn start(&self, _id: &str) -> Result<(), Self::Error> {
        unimplemented!()
    }

    async fn stop(
        &self,
        _id: &str,
        _wait_before_kill: Option<std::time::Duration>,
    ) -> Result<(), Self::Error> {
        unimplemented!()
    }

    async fn restart(&self, _id: &str) -> Result<(), Self::Error> {
        unimplemented!()
    }

    async fn remove(&self, _id: &str) -> Result<(), Self::Error> {
        unimplemented!()
    }

    async fn system_info(&self) -> Result<edgelet_core::SystemInfo, Self::Error> {
        unimplemented!()
    }

    async fn system_resources(&self) -> Result<edgelet_core::SystemResources, Self::Error> {
        unimplemented!()
    }

    async fn list(&self) -> Result<Vec<Self::Module>, Self::Error> {
        unimplemented!()
    }

    async fn logs(
        &self,
        _id: &str,
        _options: &edgelet_core::LogOptions,
    ) -> Result<hyper::Body, Self::Error> {
        unimplemented!()
    }

    async fn remove_all(&self) -> Result<(), Self::Error> {
        unimplemented!()
    }

    async fn stop_all(
        &self,
        _wait_before_kill: Option<std::time::Duration>,
    ) -> Result<(), Self::Error> {
        unimplemented!()
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        unimplemented!()
    }
}
