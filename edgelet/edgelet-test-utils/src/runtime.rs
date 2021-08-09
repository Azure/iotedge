// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, serde::Serialize)]
pub struct Config {}

pub struct Module {}

#[async_trait::async_trait]
impl edgelet_core::Module for Module {
    type Config = Config;
    type Error = std::io::Error;

    fn name(&self) -> &str {
        todo!()
    }

    fn type_(&self) -> &str {
        todo!()
    }

    fn config(&self) -> &Self::Config {
        todo!()
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

pub struct Runtime {}

#[async_trait::async_trait]
impl edgelet_core::ModuleRuntime for Runtime {
    type Error = std::io::Error;

    type Config = Config;
    type Module = Module;
    type ModuleRegistry = ModuleRegistry;

    type Chunk = bytes::Bytes;
    type Logs =
        std::pin::Pin<Box<dyn futures::Stream<Item = Result<Self::Chunk, Self::Error>> + Send>>;

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

    async fn logs(&self, id: &str, options: &edgelet_core::LogOptions) -> Self::Logs {
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

    async fn module_top(&self, id: &str) -> Result<Vec<i32>, Self::Error> {
        todo!()
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        todo!()
    }
}
