// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone)]
pub struct Module {
    pub name: String,
    pub type_: String,
    pub config: edgelet_settings::DockerConfig,
}

impl Default for Module {
    fn default() -> Self {
        let config = edgelet_settings::DockerConfig::new(
            "testImage".to_string(),
            docker::models::ContainerCreateBody::new(),
            Some("testDigest".to_string()),
            None,
        )
        .unwrap();

        Module {
            name: "testModule".to_string(),
            type_: "test".to_string(),
            config,
        }
    }
}

#[async_trait::async_trait]
impl edgelet_core::Module for Module {
    type Config = edgelet_settings::DockerConfig;
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

pub struct ModuleRegistry {}

#[async_trait::async_trait]
impl edgelet_core::ModuleRegistry for ModuleRegistry {
    type Config = edgelet_settings::DockerConfig;
    type Error = std::io::Error;

    // The fuctions below aren't used in tests.

    async fn pull(&self, _config: &Self::Config) -> Result<(), Self::Error> {
        unimplemented!()
    }

    async fn remove(&self, _name: &str) -> Result<(), Self::Error> {
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
    type Error = std::io::Error;

    type Config = edgelet_settings::DockerConfig;
    type Module = Module;
    type ModuleRegistry = ModuleRegistry;

    async fn module_top(&self, id: &str) -> Result<Vec<i32>, Self::Error> {
        if id == "runtimeError" {
            Err(crate::test_error())
        } else {
            let pids = if let Some(pids) = self.module_auth.get(id) {
                pids.clone()
            } else {
                if let Some(default) = self.module_auth.get("default") {
                    default.clone()
                } else {
                    Vec::new()
                }
            };

            Ok(pids)
        }
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

    async fn list_with_details(
        &self,
    ) -> Result<Vec<(Self::Module, edgelet_core::ModuleRuntimeState)>, Self::Error> {
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
