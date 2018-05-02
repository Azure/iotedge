// Copyright (c) Microsoft. All rights reserved.

use std::marker::PhantomData;
use edgelet_core::*;
use failure::Fail;
use futures::IntoFuture;
use futures::future::{self, FutureResult};

#[derive(Clone, Debug)]
pub struct NullRegistry<E: Fail> {
    phantom: PhantomData<E>,
}

impl<E: Fail> NullRegistry<E> {
    pub fn new() -> Self {
        NullRegistry {
            phantom: PhantomData,
        }
    }
}

impl<E: Fail> Default for NullRegistry<E> {
    fn default() -> Self {
        NullRegistry::new()
    }
}

impl<E: Fail> ModuleRegistry for NullRegistry<E> {
    type Error = E;
    type PullFuture = FutureResult<(), Self::Error>;
    type RemoveFuture = FutureResult<(), Self::Error>;
    type Config = TestConfig;

    fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
        future::ok(())
    }

    fn remove(&self, _name: &str) -> Self::RemoveFuture {
        future::ok(())
    }
}

#[derive(Clone, Debug, Serialize, Deserialize)]
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

#[derive(Clone, Debug)]
pub struct TestModule<E: Fail> {
    name: String,
    config: TestConfig,
    state: Result<ModuleRuntimeState, E>,
}

impl<E: Fail> TestModule<E> {
    pub fn new(name: String, config: TestConfig, state: Result<ModuleRuntimeState, E>) -> Self {
        TestModule {
            name,
            config,
            state,
        }
    }
}

impl<E: Clone + Fail> Module for TestModule<E> {
    type Config = TestConfig;
    type Error = E;
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
        self.state.clone().into_future()
    }
}

#[derive(Clone)]
pub struct TestRuntime<E: Fail> {
    module: Result<TestModule<E>, E>,
    registry: NullRegistry<E>,
}

impl<E: Fail> TestRuntime<E> {
    pub fn new(module: Result<TestModule<E>, E>) -> Self {
        TestRuntime {
            module,
            registry: NullRegistry::new(),
        }
    }
}

impl<E: Clone + Fail> ModuleRuntime for TestRuntime<E> {
    type Error = E;
    type Config = TestConfig;
    type Module = TestModule<E>;
    type ModuleRegistry = NullRegistry<E>;
    type CreateFuture = FutureResult<(), Self::Error>;
    type StartFuture = FutureResult<(), Self::Error>;
    type StopFuture = FutureResult<(), Self::Error>;
    type RestartFuture = FutureResult<(), Self::Error>;
    type RemoveFuture = FutureResult<(), Self::Error>;
    type ListFuture = FutureResult<Vec<Self::Module>, Self::Error>;

    fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        match self.module {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn start(&self, _id: &str) -> Self::StartFuture {
        match self.module {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn stop(&self, _id: &str) -> Self::StopFuture {
        match self.module {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn restart(&self, _id: &str) -> Self::RestartFuture {
        match self.module {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        match self.module {
            Ok(_) => future::ok(()),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn list(&self) -> Self::ListFuture {
        match self.module {
            Ok(ref m) => future::ok(vec![m.clone()]),
            Err(ref e) => future::err(e.clone()),
        }
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        &self.registry
    }
}
