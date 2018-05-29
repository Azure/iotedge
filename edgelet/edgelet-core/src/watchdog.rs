// Copyright (c) Microsoft. All rights reserved.

use error::{Error, ErrorKind};
use futures::future::{self, Either, FutureResult};
use futures::Future;
use identity::{Identity, IdentityManager, IdentitySpec};
use module::{Module, ModuleRegistry, ModuleRuntime, ModuleSpec, ModuleStatus};

/// This variable holds the generation ID associated with the Edge Agent module.
const MODULE_GENERATIONID: &str = "IOTEDGE_MODULEGENERATIONID";

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
    pub fn start(
        &mut self,
        spec: ModuleSpec<<M::Module as Module>::Config>,
        module_id: &str,
    ) -> impl Future<Item = (), Error = Error> {
        let (runtime, runtime_copy) = (self.runtime.clone(), self.runtime.clone());
        let name = spec.name().to_string();

        // Update identity of EdgeAgent to use the Auth mechanism supported by this Edgelet (Sas tokens)
        let mut id_mgr_copy = self.id_mgr.clone();
        let module_id = module_id.to_string();
        //TODO: remove edgeHub identity update when agent can update identities
        update_identity(&mut self.id_mgr, "$edgeHub")
            .and_then(move |_| update_identity(&mut id_mgr_copy, &module_id))
            .and_then(move |id| runtime.list().map(|m| (id, m)).map_err(|e| e.into()))
            .and_then(move |(id, m)| {
                m.iter()
                    .filter_map(|m| if m.name() == name { Some(m) } else { None })
                    .nth(0)
                    .map(|m| Either::A(start_runtime(runtime_copy.clone(), m)))
                    .unwrap_or_else(|| {
                        // add the generation ID for edge agent as an environment variable
                        let mut env = spec.env().clone();
                        env.insert(
                            MODULE_GENERATIONID.to_string(),
                            id.generation_id().to_string(),
                        );

                        Either::B(create_and_start(runtime_copy, spec.with_env(env)))
                    })
                    .map(|_| info!("Edge runtime started."))
            })
    }
}

// Update the edge agent identity to use the right authentication mechanism.
fn update_identity<I>(
    id_mgr: &mut I,
    module_id: &str,
) -> impl Future<Item = I::Identity, Error = Error>
where
    I: 'static + IdentityManager + Clone,
    I::Error: Into<Error>,
{
    info!("Updating identity for {}", module_id);

    let mut id_mgr_copy = id_mgr.clone();
    id_mgr
        .get(IdentitySpec::new(module_id))
        .map_err(|e| e.into())
        .and_then(move |identity| {
            identity
                .map(|module| {
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

// Check edge agent state and start container if not running.
fn start_runtime<M>(
    runtime: M,
    module: &<M as ModuleRuntime>::Module,
) -> impl Future<Item = (), Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
{
    let module_name = module.name().to_string();
    module
        .runtime_state()
        .map_err(|e| e.into())
        .and_then(move |state| match *state.status() {
            ModuleStatus::Running => future::Either::A(future::ok(())),
            _ => {
                info!("Starting edge runtime module {}", module_name);
                future::Either::B(runtime.start(&module_name).map_err(|e| e.into()))
            }
        })
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
