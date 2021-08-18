// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Default, serde::Serialize)]
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

    async fn runtime_state(&self) -> Result<edgelet_core::ModuleRuntimeState, Self::Error> {
        todo!()
    }
}

pub struct ModuleRegistry {}

#[async_trait::async_trait]
impl edgelet_core::ModuleRegistry for ModuleRegistry {
    type Config = Config;
    type Error = std::io::Error;

    async fn pull(&self, config: &Self::Config) -> Result<(), Self::Error> {
        todo!()
    }

    async fn remove(&self, name: &str) -> Result<(), Self::Error> {
        todo!()
    }
}

pub struct Runtime {
    pub module_top_resp: Option<Vec<i32>>,
}

impl Runtime {
    /// Return a generic error. Most users of ModuleRuntime don't act on the error other
    /// than passing it up the call stack, so it's fine to return any error.
    fn test_error() -> std::io::Error {
        std::io::Error::new(std::io::ErrorKind::Other, "test error")
    }

    pub fn clear_auth(&mut self) {
        // Empty PID array for auth will deny all requests.
        self.module_top_resp = Some(vec![]);
    }
}

impl Default for Runtime {
    fn default() -> Self {
        // The PID in module_top is used for auth. Bypass auth when testing by always placing
        // this process's PID in the module_top response.
        let pid = nix::unistd::getpid().as_raw();

        Runtime {
            module_top_resp: Some(vec![pid]),
        }
    }
}

#[async_trait::async_trait]
impl edgelet_core::ModuleRuntime for Runtime {
    type Error = std::io::Error;

    type Config = Config;
    type Module = Module;
    type ModuleRegistry = ModuleRegistry;

    async fn create(
        &self,
        module: edgelet_settings::ModuleSpec<Self::Config>,
    ) -> Result<(), Self::Error> {
        todo!()
    }

    async fn get(
        &self,
        id: &str,
    ) -> Result<(Self::Module, edgelet_core::ModuleRuntimeState), Self::Error> {
        todo!()
    }

    async fn start(&self, id: &str) -> Result<(), Self::Error> {
        todo!()
    }

    async fn stop(
        &self,
        id: &str,
        wait_before_kill: Option<std::time::Duration>,
    ) -> Result<(), Self::Error> {
        todo!()
    }

    async fn restart(&self, id: &str) -> Result<(), Self::Error> {
        todo!()
    }

    async fn remove(&self, id: &str) -> Result<(), Self::Error> {
        todo!()
    }

    async fn system_info(&self) -> Result<edgelet_core::SystemInfo, Self::Error> {
        todo!()
    }

    async fn system_resources(&self) -> Result<edgelet_core::SystemResources, Self::Error> {
        todo!()
    }

    async fn list(&self) -> Result<Vec<Self::Module>, Self::Error> {
        todo!()
    }

    async fn list_with_details(
        &self,
    ) -> Result<Vec<(Self::Module, edgelet_core::ModuleRuntimeState)>, Self::Error> {
        todo!()
    }

    async fn logs(
        &self,
        id: &str,
        options: &edgelet_core::LogOptions,
    ) -> Result<hyper::Body, Self::Error> {
        todo!()
    }

    async fn remove_all(&self) -> Result<(), Self::Error> {
        todo!()
    }

    async fn stop_all(
        &self,
        wait_before_kill: Option<std::time::Duration>,
    ) -> Result<(), Self::Error> {
        todo!()
    }

    async fn module_top(&self, _id: &str) -> Result<Vec<i32>, Self::Error> {
        if let Some(resp) = &self.module_top_resp {
            Ok(resp.clone())
        } else {
            Err(Self::test_error())
        }
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        todo!()
    }
}
