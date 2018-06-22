// Copyright (c) Microsoft. All rights reserved.

use std::time::{Duration, Instant};

use edgelet_utils::log_failure;
use futures::future::{self, Either, FutureResult};
use futures::Future;
use log::Level;
use tokio::prelude::*;
use tokio::timer::Interval;

use error::{Error, ErrorKind};
use identity::{Identity, IdentityManager, IdentitySpec};
use module::{Module, ModuleRegistry, ModuleRuntime, ModuleSpec, ModuleStatus};

// Time to allow EdgeAgent to gracefully shutdown (including stopping all modules, and updating reported properties)
const EDGE_RUNTIME_STOP_TIME: Duration = Duration::from_secs(60);

/// This variable holds the generation ID associated with the Edge Agent module.
const MODULE_GENERATIONID: &str = "IOTEDGE_MODULEGENERATIONID";

/// This is the frequency with which the watchdog checks for the status of the edge runtime module.
const WATCHDOG_FREQUENCY_SECS: u64 = 60;

pub struct Watchdog<M, I> {
    runtime: M,
    id_mgr: I,
}

impl<M, I> Watchdog<M, I>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
    I: 'static + IdentityManager + Clone,
    I::Error: Into<Error>,
{
    pub fn new(runtime: M, id_mgr: I) -> Self {
        Watchdog { runtime, id_mgr }
    }

    // Start the edge runtime module (EdgeAgent). This also updates the identity of the module (module_id)
    // to make sure it is configured for the right authentication type (sas token)
    // spec.name = edgeAgent / module_id = $edgeAgent
    pub fn run_until<F>(
        self,
        spec: ModuleSpec<<M::Module as Module>::Config>,
        module_id: &str,
        shutdown_signal: F,
    ) -> impl Future<Item = (), Error = Error>
    where
        F: Future<Item = (), Error = ()> + 'static,
    {
        let runtime = self.runtime.clone();
        let runtime_copy = self.runtime.clone();
        let name = spec.name().to_string();
        let mut id_mgr = self.id_mgr.clone();
        let module_id = module_id.to_string();

        // Check if the Edge runtime module exists, and if not create it
        let watchdog = get_edge_runtime_mod(&runtime, name.clone())
            .and_then(move |m| {
                // If the module doesn't exist, update the cloud with its identity and stash its
                // generation id (obtained from the cloud) in an environment object. The environment is
                // then injected in to the new module at creation time. If the module already
                // exists, just start it.
                if m.is_none() {
                    let update = update_identity(&mut id_mgr, &module_id).and_then(|id| {
                        // add the generation ID for edge agent as an environment variable
                        let mut env = spec.env().clone();
                        env.insert(
                            MODULE_GENERATIONID.to_string(),
                            id.generation_id().to_string(),
                        );
                        future::ok(spec.with_env(env))
                    });
                    Either::A(update)
                } else {
                    Either::B(future::ok(spec))
                }
            })
            .and_then(|spec| start_watchdog(runtime, spec));

        // Swallow any errors from shutdown_signal
        let shutdown_signal = shutdown_signal.then(|_| Ok(()));

        // Wait for the watchdog or shutdown futures to complete
        // Since the watchdog never completes, this will wait for the
        // shutdown signal.
        shutdown_signal
            .select(watchdog)
            .then(move |result| match result {
                Ok(((), _)) => Either::A(stop_runtime(&runtime_copy, &name)),
                Err((e, _)) => Either::B(future::err(e)),
            })
    }
}

// Stop EdgeAgent
fn stop_runtime<M>(runtime: &M, name: &str) -> impl Future<Item = (), Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
{
    info!("Stopping edge runtime module {}", name);
    runtime
        .stop(name, Some(EDGE_RUNTIME_STOP_TIME))
        .map_err(|e| e.into())
}

// Start watchdog on a timer for 1 minute
pub fn start_watchdog<M>(
    runtime: M,
    spec: ModuleSpec<<M::Module as Module>::Config>,
) -> impl Future<Item = (), Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
{
    info!(
        "Starting watchdog with {} second frequency...",
        WATCHDOG_FREQUENCY_SECS
    );
    Interval::new(Instant::now(), Duration::from_secs(WATCHDOG_FREQUENCY_SECS))
        .map_err(Error::from)
        .for_each(move |_| {
            info!("Checking edge runtime status");
            check_runtime(runtime.clone(), spec.clone()).or_else(|e| {
                warn!("Error in watchdog when checking for edge runtime status:");
                log_failure(Level::Warn, &e);
                future::ok(())
            })
        })
}

// Check if the edge runtime module is running, and if not, start it.
fn check_runtime<M>(
    runtime: M,
    spec: ModuleSpec<<M::Module as Module>::Config>,
) -> impl Future<Item = (), Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
{
    let module = spec.name().to_string();
    get_edge_runtime_mod(&runtime, module.clone())
        .and_then(|m| m.map(|m| m.runtime_state().map_err(|e| e.into())))
        .and_then(move |state| {
            state
                .map(|state| {
                    let res = match *state.status() {
                        ModuleStatus::Running => {
                            info!("Edge runtime is running.");
                            future::Either::A(future::ok(()))
                        }
                        _ => {
                            info!(
                                "Edge runtime status is {}, starting module now...",
                                *state.status()
                            );
                            future::Either::B(runtime.start(&module).map_err(|e| e.into()))
                        }
                    };
                    Either::A(res)
                })
                .unwrap_or_else(|| Either::B(create_and_start(runtime, spec)))
        })
        .map(|_| ())
}

// Gets the edge runtime module, if it exists.
fn get_edge_runtime_mod<M>(
    runtime: &M,
    name: String,
) -> impl Future<Item = Option<M::Module>, Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
{
    runtime
        .list()
        .map(move |m| {
            m.into_iter()
                .filter_map(move |m| if m.name() == name { Some(m) } else { None })
                .nth(0)
        })
        .map_err(|e| e.into())
}

// Gets the identity for the module.
// If the update flag is specified, then also updates the identity of the module.
fn update_identity<I>(
    id_mgr: &mut I,
    module_id: &str,
) -> impl Future<Item = I::Identity, Error = Error>
where
    I: 'static + IdentityManager + Clone,
    I::Error: Into<Error>,
{
    let mut id_mgr_copy = id_mgr.clone();
    id_mgr
        .get(IdentitySpec::new(module_id))
        .map_err(|e| e.into())
        .and_then(move |identity| {
            identity
                .map(|module| {
                    info!("Updating identity for module {}", module.module_id());
                    let res = id_mgr_copy
                        .update(
                            IdentitySpec::new(module.module_id())
                                .with_generation_id(module.generation_id().to_string()),
                        )
                        .map_err(|e| e.into());
                    Either::A(res)
                })
                .unwrap_or_else(|| {
                    Either::B(future::err(Error::from(ErrorKind::EdgeRuntimeNotFound))
                        as FutureResult<I::Identity, Error>)
                })
        })
}

// Edge agent does not exist - pull, create and start the container
fn create_and_start<M>(
    runtime: M,
    spec: ModuleSpec<<M::Module as Module>::Config>,
) -> impl Future<Item = (), Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
{
    let module_name = spec.name().to_string();
    info!("Creating and starting edge runtime module {}", module_name);
    let runtime_copy = runtime.clone();

    runtime
        .registry()
        .pull(spec.clone().config())
        .and_then(move |_| runtime.create(spec))
        .and_then(move |_| runtime_copy.start(&module_name))
        .map_err(|e| e.into())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::cell::RefCell;
    use std::rc::Rc;

    use futures::future::{self, FutureResult};

    use error::{Error as CoreError, ErrorKind as CoreErrorKind};
    use identity::{AuthType, Identity, IdentityManager, IdentitySpec};

    #[derive(Clone, Debug, Fail)]
    pub enum Error {
        #[fail(display = "General error")]
        General,

        #[fail(display = "Module not found")]
        ModuleNotFound,
    }

    impl From<Error> for CoreError {
        fn from(_err: Error) -> CoreError {
            CoreError::from(CoreErrorKind::Identity)
        }
    }

    #[derive(Clone, Debug, Deserialize, Serialize)]
    pub struct TestIdentity {
        #[serde(rename = "moduleId")]
        module_id: String,
        #[serde(rename = "managedBy")]
        managed_by: String,
        #[serde(rename = "generationId")]
        generation_id: String,
        #[serde(rename = "authType")]
        auth_type: AuthType,
    }

    impl TestIdentity {
        pub fn new(
            module_id: &str,
            managed_by: &str,
            generation_id: &str,
            auth_type: AuthType,
        ) -> TestIdentity {
            TestIdentity {
                module_id: module_id.to_string(),
                managed_by: managed_by.to_string(),
                generation_id: generation_id.to_string(),
                auth_type,
            }
        }
    }

    impl Identity for TestIdentity {
        fn module_id(&self) -> &str {
            &self.module_id
        }

        fn managed_by(&self) -> &str {
            &self.managed_by
        }

        fn generation_id(&self) -> &str {
            &self.generation_id
        }

        fn auth_type(&self) -> AuthType {
            self.auth_type.clone()
        }
    }

    struct State {
        identities: Vec<TestIdentity>,
        gen_id_sentinel: u32,
        fail_get: bool,
        fail_update: bool,
        update_called: bool,
    }

    #[derive(Clone)]
    pub struct TestIdentityManager {
        state: Rc<RefCell<State>>,
    }

    impl TestIdentityManager {
        pub fn new(identities: Vec<TestIdentity>) -> TestIdentityManager {
            TestIdentityManager {
                state: Rc::new(RefCell::new(State {
                    identities,
                    gen_id_sentinel: 0,
                    fail_get: false,
                    fail_update: false,
                    update_called: false,
                })),
            }
        }

        pub fn with_fail_get(self, fail_get: bool) -> TestIdentityManager {
            self.state.borrow_mut().fail_get = fail_get;
            self
        }

        pub fn with_fail_update(self, fail_update: bool) -> TestIdentityManager {
            self.state.borrow_mut().fail_update = fail_update;
            self
        }
    }

    impl IdentityManager for TestIdentityManager {
        type Identity = TestIdentity;
        type Error = Error;
        type CreateFuture = FutureResult<Self::Identity, Self::Error>;
        type UpdateFuture = FutureResult<Self::Identity, Self::Error>;
        type ListFuture = FutureResult<Vec<Self::Identity>, Self::Error>;
        type GetFuture = FutureResult<Option<Self::Identity>, Self::Error>;
        type DeleteFuture = FutureResult<(), Self::Error>;

        fn create(&mut self, id: IdentitySpec) -> Self::CreateFuture {
            self.state.borrow_mut().gen_id_sentinel += 1;
            let id = TestIdentity::new(
                id.module_id(),
                "iotedge",
                &format!("{}", self.state.borrow().gen_id_sentinel),
                AuthType::Sas,
            );
            self.state.borrow_mut().identities.push(id.clone());

            future::ok(id)
        }

        fn update(&mut self, id: IdentitySpec) -> Self::UpdateFuture {
            self.state.borrow_mut().update_called = true;

            if self.state.borrow().fail_update {
                future::err(Error::General)
            } else {
                // find the existing module
                let index = self.state
                    .borrow()
                    .identities
                    .iter()
                    .position(|m| m.module_id() == id.module_id())
                    .unwrap();

                let mut module = self.state.borrow().identities[index].clone();

                // verify if genid matches
                assert_eq!(&module.generation_id, id.generation_id().unwrap());

                // set the sas type
                module.auth_type = AuthType::Sas;

                // delete/insert updated module
                self.state.borrow_mut().identities.remove(index);
                self.state.borrow_mut().identities.push(module.clone());

                future::ok(module)
            }
        }

        fn list(&self) -> Self::ListFuture {
            future::ok(self.state.borrow().identities.clone())
        }

        fn get(&self, id: IdentitySpec) -> Self::GetFuture {
            if self.state.borrow().fail_get {
                future::err(Error::General)
            } else {
                match self.state
                    .borrow()
                    .identities
                    .iter()
                    .find(|m| m.module_id() == id.module_id())
                {
                    Some(module) => future::ok(Some(module.clone())),
                    None => future::err(Error::ModuleNotFound),
                }
            }
        }

        fn delete(&mut self, id: IdentitySpec) -> Self::DeleteFuture {
            self.state
                .borrow()
                .identities
                .iter()
                .position(|ref mid| mid.module_id() == id.module_id())
                .map(|index| self.state.borrow_mut().identities.remove(index))
                .map(|_| future::ok(()))
                .unwrap_or_else(|| future::err(Error::ModuleNotFound))
        }
    }

    #[test]
    fn update_identity_get_fails() {
        let mut manager = TestIdentityManager::new(vec![]).with_fail_get(true);
        assert_eq!(
            true,
            update_identity(&mut manager, "$edgeAgent").wait().is_err()
        );
    }

    #[test]
    fn update_identity_update_fails() {
        let mut manager = TestIdentityManager::new(vec![TestIdentity::new(
            "$edgeAgent",
            "iotedge",
            "1",
            AuthType::None,
        )]).with_fail_update(true);

        assert_eq!(
            true,
            update_identity(&mut manager, "$edgeAgent").wait().is_err()
        );
        assert_eq!(true, manager.state.borrow().update_called);
    }

    #[test]
    fn update_identity_succeeds() {
        let mut manager = TestIdentityManager::new(vec![TestIdentity::new(
            "$edgeAgent",
            "iotedge",
            "1",
            AuthType::None,
        )]);

        assert_eq!(
            false,
            update_identity(&mut manager, "$edgeAgent").wait().is_err()
        );
        assert_eq!(true, manager.state.borrow().update_called);
        assert_eq!(
            AuthType::Sas,
            manager
                .get(IdentitySpec::new("$edgeAgent"))
                .wait()
                .unwrap()
                .unwrap()
                .auth_type
        );
    }
}
