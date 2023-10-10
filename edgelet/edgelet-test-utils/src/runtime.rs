// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Default, serde::Deserialize, serde::Serialize)]
pub struct Config {}

#[derive(Clone)]
pub struct Module {
    pub name: String,
    pub type_: String,
    pub config: Config,
}

impl Default for Module {
    fn default() -> Self {
        Module {
            name: "testModule".to_string(),
            type_: "test".to_string(),
            config: Config::default(),
        }
    }
}

#[async_trait::async_trait]
impl edgelet_core::Module for Module {
    type Config = Config;

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

    async fn runtime_state(&self) -> anyhow::Result<edgelet_core::ModuleRuntimeState> {
        unimplemented!()
    }
}

pub struct ModuleRegistry {}

#[async_trait::async_trait]
impl edgelet_core::ModuleRegistry for ModuleRegistry {
    type Config = Config;

    // The fuctions below aren't used in tests.

    async fn pull(&self, _config: &Self::Config) -> anyhow::Result<()> {
        unimplemented!()
    }

    async fn remove(&self, _name: &str) -> anyhow::Result<()> {
        unimplemented!()
    }
}

pub struct Runtime {
    pub module_auth: std::collections::BTreeMap<String, Vec<i32>>,
}

impl Default for Runtime {
    fn default() -> Self {
        // The PID in module_top is used for auth. Bypass auth when testing by always placing
        // this process's PID in the default module_top response.
        let pid = nix::unistd::getpid().as_raw();

        let mut modules = std::collections::BTreeMap::new();
        modules.insert("default".to_string(), vec![pid]);

        Runtime {
            module_auth: modules,
        }
    }
}

#[async_trait::async_trait]
impl edgelet_core::ModuleRuntime for Runtime {
    type Config = Config;
    type Module = Module;
    type ModuleRegistry = ModuleRegistry;

    async fn module_top(&self, id: &str) -> anyhow::Result<Vec<i32>> {
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

    // The functions below aren't used in tests.

    async fn create(
        &self,
        _module: edgelet_settings::ModuleSpec<Self::Config>,
    ) -> anyhow::Result<()> {
        unimplemented!()
    }

    async fn get(
        &self,
        _id: &str,
    ) -> anyhow::Result<(Self::Module, edgelet_core::ModuleRuntimeState)> {
        unimplemented!()
    }

    async fn start(&self, _id: &str) -> anyhow::Result<()> {
        unimplemented!()
    }

    async fn stop(
        &self,
        _id: &str,
        _wait_before_kill: Option<std::time::Duration>,
    ) -> anyhow::Result<()> {
        unimplemented!()
    }

    async fn restart(&self, _id: &str) -> anyhow::Result<()> {
        unimplemented!()
    }

    async fn remove(&self, _id: &str) -> anyhow::Result<()> {
        unimplemented!()
    }

    async fn system_info(&self) -> anyhow::Result<edgelet_core::SystemInfo> {
        unimplemented!()
    }

    async fn system_resources(&self) -> anyhow::Result<edgelet_core::SystemResources> {
        unimplemented!()
    }

    async fn list(&self) -> anyhow::Result<Vec<Self::Module>> {
        unimplemented!()
    }

    async fn list_with_details(
        &self,
    ) -> anyhow::Result<Vec<(Self::Module, edgelet_core::ModuleRuntimeState)>> {
        unimplemented!()
    }

    async fn list_images(&self) -> anyhow::Result<std::collections::HashMap<String, String>> {
        unimplemented!()
    }

    async fn logs(
        &self,
        _id: &str,
        _options: &edgelet_core::LogOptions,
    ) -> anyhow::Result<hyper::Body> {
        unimplemented!()
    }

    async fn remove_all(&self) -> anyhow::Result<()> {
        unimplemented!()
    }

    async fn stop_all(&self, _wait_before_kill: Option<std::time::Duration>) -> anyhow::Result<()> {
        unimplemented!()
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        unimplemented!()
    }

    fn error_code(_error: &anyhow::Error) -> hyper::StatusCode {
        unimplemented!()
    }
}
