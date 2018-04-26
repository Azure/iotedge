// Copyright (c) Microsoft. All rights reserved.

use std::rc::Rc;
use std::str::FromStr;

use edgelet_core::*;
use futures::Future;
use futures::future::{self, FutureResult};
use hyper::client::Connect;
use management::apis::client::APIClient;
use management::models::{Config, ModuleDetails as HttpModuleDetails};

use error::Error;

pub struct ModuleClient<C: Connect> {
    client: Rc<APIClient<C>>,
}

impl<C: Connect> ModuleClient<C> {
    pub fn new(client: APIClient<C>) -> ModuleClient<C> {
        ModuleClient {
            client: Rc::new(client),
        }
    }
}

impl<C: Connect> Clone for ModuleClient<C> {
    fn clone(&self) -> Self {
        ModuleClient {
            client: self.client.clone(),
        }
    }
}

#[derive(Clone, Debug)]
pub struct ModuleDetails(HttpModuleDetails);

impl Module for ModuleDetails {
    type Config = Config;
    type Error = Error;
    type RuntimeStateFuture = FutureResult<ModuleRuntimeState, Self::Error>;

    fn name(&self) -> &str {
        self.0.name()
    }

    fn type_(&self) -> &str {
        self.0.type_()
    }

    fn config(&self) -> &Self::Config {
        self.0.config()
    }

    fn runtime_state(&self) -> Self::RuntimeStateFuture {
        future::result(runtime_status(&self.0))
    }
}

fn runtime_status(details: &HttpModuleDetails) -> Result<ModuleRuntimeState, Error> {
    let status = ModuleStatus::from_str(details.status().runtime_status().status())?;
    let description = details.status().runtime_status().description().cloned();
    let exit_code = details
        .status()
        .exit_status()
        .and_then(|e| e.status_code().parse::<i32>().ok());
    let exit_time = details
        .status()
        .exit_status()
        .and_then(|e| e.exit_time().parse().ok());
    let start_time = details.status().start_time().and_then(|s| s.parse().ok());

    let state = ModuleRuntimeState::default()
        .with_status(status)
        .with_status_description(description)
        .with_exit_code(exit_code)
        .with_started_at(start_time)
        .with_finished_at(exit_time);
    Ok(state)
}

impl<C: Connect> ModuleRegistry for ModuleClient<C> {
    type Error = Error;
    type PullFuture = FutureResult<(), Self::Error>;
    type RemoveFuture = FutureResult<(), Self::Error>;
    type Config = Config;

    fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
        future::ok(())
    }

    fn remove(&self, _name: &str) -> Self::RemoveFuture {
        future::ok(())
    }
}

impl<C: Connect> ModuleRuntime for ModuleClient<C> {
    type Error = Error;
    type Config = Config;
    type Module = ModuleDetails;
    type ModuleRegistry = Self;

    type CreateFuture = Box<Future<Item = (), Error = Self::Error>>;
    type StartFuture = Box<Future<Item = (), Error = Self::Error>>;
    type StopFuture = Box<Future<Item = (), Error = Self::Error>>;
    type RestartFuture = Box<Future<Item = (), Error = Self::Error>>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error>>;
    type ListFuture = Box<Future<Item = Vec<Self::Module>, Error = Self::Error>>;

    fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        unimplemented!()
    }

    fn start(&self, _id: &str) -> Self::StartFuture {
        unimplemented!()
    }

    fn stop(&self, _id: &str) -> Self::StopFuture {
        unimplemented!()
    }

    fn restart(&self, _id: &str) -> Self::RestartFuture {
        unimplemented!()
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        unimplemented!()
    }

    fn list(&self) -> Self::ListFuture {
        let modules = self.client
            .module_api()
            .list_modules("2018-06-28")
            .map(|list| {
                list.modules()
                    .into_iter()
                    .cloned()
                    .map(ModuleDetails)
                    .collect()
            })
            .map_err(From::from);
        Box::new(modules)
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        self
    }
}
