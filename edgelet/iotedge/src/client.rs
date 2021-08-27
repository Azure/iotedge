use edgelet_core::{
    LogOptions, Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, SystemInfo,
    SystemResources,
};
use edgelet_settings::module::Settings as ModuleSpec;
use std::time::Duration;

use crate::Error;
type Result<T> = std::result::Result<T, Error>;

struct MgmtClient {}

impl MgmtClient {
    fn new() -> Self {
        Self {}
    }
}

#[derive(serde::Serialize, Clone)]
struct NullConfig {}

struct ApiModule {
    name: String,
    type_: String,
}

#[async_trait::async_trait]
impl ModuleRuntime for MgmtClient {
    type Error = Error;
    type Config = NullConfig;
    type Module = ApiModule;
    type ModuleRegistry = Self;

    async fn create(&self, module: ModuleSpec<Self::Config>) -> Result<()> {
        unimplemented!()
    }
    async fn get(&self, id: &str) -> Result<(Self::Module, ModuleRuntimeState)> {
        unimplemented!()
    }
    async fn start(&self, id: &str) -> Result<()> {
        unimplemented!()
    }
    async fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> Result<()> {
        unimplemented!()
    }
    async fn restart(&self, id: &str) -> Result<()> {
        unimplemented!()
    }
    async fn remove(&self, id: &str) -> Result<()> {
        unimplemented!()
    }
    async fn system_info(&self) -> Result<SystemInfo> {
        unimplemented!()
    }
    async fn system_resources(&self) -> Result<SystemResources> {
        unimplemented!()
    }
    async fn list(&self) -> Result<Vec<Self::Module>> {
        unimplemented!()
    }
    async fn list_with_details(&self) -> Result<Vec<(Self::Module, ModuleRuntimeState)>> {
        unimplemented!()
    }
    async fn logs(&self, id: &str, options: &LogOptions) -> Result<hyper::Body> {
        unimplemented!()
    }
    async fn remove_all(&self) -> Result<()> {
        unimplemented!()
    }
    async fn stop_all(&self, wait_before_kill: Option<Duration>) -> Result<()> {
        unimplemented!()
    }
    async fn module_top(&self, id: &str) -> Result<Vec<i32>> {
        unimplemented!()
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        unimplemented!()
    }
}

#[async_trait::async_trait]
impl Module for ApiModule {
    type Config = NullConfig;
    type Error = Error;

    fn name(&self) -> &str {
        &self.name
    }
    fn type_(&self) -> &str {
        &self.type_
    }
    fn config(&self) -> &Self::Config {
        &NullConfig {}
    }

    async fn runtime_state(&self) -> Result<ModuleRuntimeState> {
        unimplemented!()
    }
}

#[async_trait::async_trait]
impl ModuleRegistry for MgmtClient {
    type Error = Error;
    type Config = NullConfig;

    async fn pull(&self, _config: &Self::Config) -> Result<()> {
        Ok(())
    }

    async fn remove(&self, _name: &str) -> Result<()> {
        Ok(())
    }
}
