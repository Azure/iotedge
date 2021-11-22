use std::{marker::PhantomData, sync::Mutex};

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
    pub pulls: Mutex<Vec<C>>,
    pub removes: Mutex<Vec<String>>,
}
pub type ModuleRegistry = TestModuleRegistry<Config>;

#[async_trait::async_trait]
impl<C> edgelet_core::ModuleRegistry for TestModuleRegistry<C>
where
    C: Send + Sync + Clone,
{
    type Config = C;
    type Error = std::io::Error;

    async fn pull(&self, config: &C) -> Result<(), Self::Error> {
        let mut pulls = self.pulls.lock().expect("Could not aquire pulls mutex");
        pulls.push(config.to_owned());
        Ok(())
    }

    async fn remove(&self, name: &str) -> Result<(), Self::Error> {
        let mut removes = self.removes.lock().expect("Could not aquire removes mutex");
        removes.push(name.to_owned());
        Ok(())
    }
}

pub struct TestRuntime<C> {
    pub module_auth: std::collections::BTreeMap<String, Vec<i32>>,
    pub module_details: Vec<(TestModule<C>, edgelet_core::ModuleRuntimeState)>,
    pub created: Mutex<Vec<edgelet_settings::ModuleSpec<C>>>,
    pub started: Mutex<Vec<String>>,
    pub stopped: Mutex<Vec<String>>,
    pub removed: Mutex<Vec<String>>,
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
            created: Mutex::new(Vec::new()),
            started: Mutex::new(Vec::new()),
            stopped: Mutex::new(Vec::new()),
            removed: Mutex::new(Vec::new()),
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

    async fn create(
        &self,
        module: edgelet_settings::ModuleSpec<Self::Config>,
    ) -> Result<(), Self::Error> {
        let mut created = self.created.lock().expect("Could not aquire created mutex");
        created.push(module);
        Ok(())
    }

    async fn start(&self, id: &str) -> Result<(), Self::Error> {
        let mut started = self.started.lock().expect("Could not aquire started mutex");
        started.push(id.to_owned());
        Ok(())
    }

    async fn stop(
        &self,
        id: &str,
        _wait_before_kill: Option<std::time::Duration>,
    ) -> Result<(), Self::Error> {
        let mut stopped = self.stopped.lock().expect("Could not aquire stopped mutex");
        stopped.push(id.to_owned());
        Ok(())
    }

    async fn remove(&self, id: &str) -> Result<(), Self::Error> {
        let mut removed = self.removed.lock().expect("Could not aquire removed mutex");
        removed.push(id.to_owned());
        Ok(())
    }

    // The functions below aren't used in tests.

    async fn get(
        &self,
        _id: &str,
    ) -> Result<(Self::Module, edgelet_core::ModuleRuntimeState), Self::Error> {
        unimplemented!()
    }

    async fn restart(&self, _id: &str) -> Result<(), Self::Error> {
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
