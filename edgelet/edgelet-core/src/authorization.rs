// Copyright (c) Microsoft. All rights reserved.

use error::{Error, ErrorKind};
use futures::future::Either;
use futures::{future, Future};
use module::{Module, ModuleRuntime};

pub enum Policy {
    Anonymous,
    Caller,
    Module(String),
}

pub struct Authorization<M>
where
    M: 'static + ModuleRuntime,
{
    runtime: M,
    policy: Policy,
}

impl<M> Authorization<M>
where
    M: 'static + ModuleRuntime,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
{
    pub fn new(runtime: M, policy: Policy) -> Self {
        Authorization { runtime, policy }
    }

    pub fn authorize(&self, name: &str, pid: i32) -> impl Future<Item = bool, Error = Error> {
        match self.policy {
            Policy::Anonymous => Either::A(Either::A(self.auth_anonymous())),
            Policy::Caller => Either::A(Either::B(self.auth_caller(name, pid))),
            Policy::Module(ref expected_name) => {
                Either::B(self.auth_module(expected_name, name, pid))
            }
        }
    }

    fn auth_anonymous(&self) -> impl Future<Item = bool, Error = Error> {
        future::ok(true)
    }

    fn auth_caller(&self, name: &str, pid: i32) -> impl Future<Item = bool, Error = Error> {
        let name = name.to_string();
        self.runtime
            .list()
            .map_err(|e| e.into())
            .and_then(move |list| {
                list.iter()
                    .filter_map(|m| if m.name() == name { Some(m) } else { None })
                    .nth(0)
                    .map(|m| {
                        Either::A(m.runtime_state().map_err(|e| e.into()).and_then(move |rs| {
                            rs.pid()
                                .ok_or_else(|| Error::from(ErrorKind::ModuleRuntimeNoPid))
                                .map(|p| *p == pid)
                        }))
                    })
                    .unwrap_or_else(|| Either::B(future::ok(false)))
            })
    }

    fn auth_module(
        &self,
        expected_name: &str,
        name: &str,
        pid: i32,
    ) -> impl Future<Item = bool, Error = Error> {
        if expected_name != name {
            return Either::A(future::ok(false));
        }

        Either::B(self.auth_caller(name, pid))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use error::{Error, ErrorKind};
    use failure::Context;
    use futures::future;
    use futures::future::FutureResult;
    use module::{Module, ModuleRegistry, ModuleRuntimeState, ModuleSpec};

    #[test]
    fn should_authorize_anonymous() {
        let runtime = TestModuleList::new(&vec![]);
        let auth = Authorization::new(runtime, Policy::Anonymous);
        assert_eq!(true, auth.authorize("don't care", 0).wait().unwrap());
    }

    #[test]
    fn should_authorize_caller() {
        let runtime = TestModuleList::new(&vec![
            TestModule::new("xyz", 987),
            TestModule::new("abc", 123),
        ]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(true, auth.authorize("abc", 123).wait().unwrap());
    }

    #[test]
    fn should_reject_caller_with_different_name() {
        let runtime = TestModuleList::new(&vec![TestModule::new("abc", 123)]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(false, auth.authorize("xyz", 123).wait().unwrap());
    }

    #[test]
    fn should_reject_caller_with_different_pid() {
        let runtime = TestModuleList::new(&vec![TestModule::new("abc", 123)]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(false, auth.authorize("abc", 456).wait().unwrap());
    }

    #[test]
    fn should_authorize_module() {
        let runtime = TestModuleList::new(&vec![
            TestModule::new("xyz", 987),
            TestModule::new("abc", 123),
        ]);
        let auth = Authorization::new(runtime, Policy::Module("abc".to_string()));
        assert_eq!(true, auth.authorize("abc", 123).wait().unwrap());
    }

    #[test]
    fn should_reject_module_whose_name_does_not_match_policy() {
        let runtime = TestModuleList::new(&vec![TestModule::new("xyz", 123)]);
        let auth = Authorization::new(runtime, Policy::Module("abc".to_string()));
        assert_eq!(false, auth.authorize("xyz", 123).wait().unwrap());
    }

    #[test]
    fn should_reject_module_with_different_name() {
        let runtime = TestModuleList::new(&vec![TestModule::new("abc", 123)]);
        let auth = Authorization::new(runtime, Policy::Module("abc".to_string()));
        assert_eq!(false, auth.authorize("xyz", 123).wait().unwrap());
    }

    #[test]
    fn should_reject_module_with_different_pid() {
        let runtime = TestModuleList::new(&vec![TestModule::new("abc", 123)]);
        let auth = Authorization::new(runtime, Policy::Module("abc".to_string()));
        assert_eq!(false, auth.authorize("abc", 456).wait().unwrap());
    }

    #[test]
    #[should_panic(expected = "Module runtime returned module information without pid.")]
    fn should_fail_when_runtime_returns_no_pid() {
        let runtime = TestModuleList::new(&vec![TestModule::new_with_behavior(
            "abc",
            123,
            TestModuleBehavior::NoPid,
        )]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(true, auth.authorize("abc", 123).wait().unwrap());
    }

    #[test]
    #[should_panic(expected = "A module runtime error occurred.")]
    fn should_fail_when_runtime_state_fails() {
        let runtime = TestModuleList::new(&vec![TestModule::new_with_behavior(
            "abc",
            123,
            TestModuleBehavior::FailRuntimeState,
        )]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(true, auth.authorize("abc", 123).wait().unwrap());
    }

    #[test]
    #[should_panic(expected = "A module runtime error occurred.")]
    fn should_fail_when_list_fails() {
        let runtime = TestModuleList::new_with_behavior(
            &vec![TestModule::new("abc", 123)],
            TestModuleListBehavior::FailList,
        );
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(true, auth.authorize("abc", 123).wait().unwrap());
    }

    struct TestConfig {}

    #[derive(Clone)]
    enum TestModuleBehavior {
        Default,
        FailRuntimeState,
        NoPid,
    }

    #[derive(Clone)]
    struct TestModule {
        name: String,
        pid: i32,
        behavior: TestModuleBehavior,
    }

    impl TestModule {
        pub fn new(name: &str, pid: i32) -> Self {
            let name = name.to_string();
            TestModule {
                name,
                pid,
                behavior: TestModuleBehavior::Default,
            }
        }

        pub fn new_with_behavior(name: &str, pid: i32, behavior: TestModuleBehavior) -> Self {
            let name = name.to_string();
            TestModule {
                name,
                pid,
                behavior,
            }
        }
    }

    macro_rules! notimpl_error {
        () => {
            future::err(Error::new(Context::new(ErrorKind::ModuleRuntime)))
        };
    }

    impl Module for TestModule {
        type Config = TestConfig;
        type Error = Error;
        type RuntimeStateFuture = FutureResult<ModuleRuntimeState, Self::Error>;

        fn name(&self) -> &str {
            &self.name
        }
        fn type_(&self) -> &str {
            ""
        }
        fn config(&self) -> &Self::Config {
            &TestConfig {}
        }
        fn runtime_state(&self) -> Self::RuntimeStateFuture {
            match self.behavior {
                TestModuleBehavior::Default => {
                    future::ok(ModuleRuntimeState::default().with_pid(Some(self.pid)))
                }
                TestModuleBehavior::FailRuntimeState => notimpl_error!(),
                TestModuleBehavior::NoPid => future::ok(ModuleRuntimeState::default()),
            }
        }
    }

    #[derive(Clone)]
    enum TestModuleListBehavior {
        Default,
        FailList,
    }

    #[derive(Clone)]
    struct TestModuleList {
        modules: Vec<TestModule>,
        behavior: TestModuleListBehavior,
    }

    impl TestModuleList {
        pub fn new(modules: &Vec<TestModule>) -> Self {
            TestModuleList {
                modules: modules.clone(),
                behavior: TestModuleListBehavior::Default,
            }
        }

        pub fn new_with_behavior(
            modules: &Vec<TestModule>,
            behavior: TestModuleListBehavior,
        ) -> Self {
            TestModuleList {
                modules: modules.clone(),
                behavior,
            }
        }
    }

    impl ModuleRegistry for TestModuleList {
        type Config = TestConfig;
        type Error = Error;
        type PullFuture = FutureResult<(), Self::Error>;
        type RemoveFuture = FutureResult<(), Self::Error>;

        fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
            notimpl_error!()
        }
        fn remove(&self, _name: &str) -> Self::RemoveFuture {
            notimpl_error!()
        }
    }

    impl ModuleRuntime for TestModuleList {
        type Error = Error;
        type Config = TestConfig;
        type Module = TestModule;
        type ModuleRegistry = Self;
        type CreateFuture = FutureResult<(), Self::Error>;
        type InitFuture = FutureResult<(), Self::Error>;
        type ListFuture = FutureResult<Vec<Self::Module>, Self::Error>;
        type RemoveFuture = FutureResult<(), Self::Error>;
        type RestartFuture = FutureResult<(), Self::Error>;
        type StartFuture = FutureResult<(), Self::Error>;
        type StopFuture = FutureResult<(), Self::Error>;

        fn init(&self) -> Self::InitFuture {
            notimpl_error!()
        }
        fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
            notimpl_error!()
        }
        fn start(&self, _id: &str) -> Self::StartFuture {
            notimpl_error!()
        }
        fn stop(&self, _id: &str) -> Self::StopFuture {
            notimpl_error!()
        }
        fn restart(&self, _id: &str) -> Self::RestartFuture {
            notimpl_error!()
        }
        fn remove(&self, _id: &str) -> Self::RemoveFuture {
            notimpl_error!()
        }
        fn list(&self) -> Self::ListFuture {
            match self.behavior {
                TestModuleListBehavior::Default => future::ok(self.modules.clone()),
                TestModuleListBehavior::FailList => notimpl_error!(),
            }
        }
        fn registry(&self) -> &Self::ModuleRegistry {
            self
        }
    }
}
