// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;
use futures::future::Either;
use futures::{future, Future, Stream};

use error::{Error, ErrorKind};
use module::{Module, ModuleRuntime};
use pid::Pid;

use super::Policy;

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
{
    pub fn new(runtime: M, policy: Policy) -> Self {
        Authorization { runtime, policy }
    }

    pub fn authorize(
        &self,
        name: Option<String>,
        pid: Pid,
    ) -> impl Future<Item = bool, Error = Error> {
        let name = name.map(|n| n.trim_left_matches('$').to_string());
        match self.policy {
            Policy::Anonymous => Either::A(Either::A(self.auth_anonymous())),
            Policy::Caller => Either::A(Either::B(self.auth_caller(name, pid))),
            Policy::Module(ref expected_name) => Either::B(self.auth_module(expected_name, pid)),
        }
    }

    fn auth_anonymous(&self) -> impl Future<Item = bool, Error = Error> {
        future::ok(true)
    }

    fn auth_caller(
        &self,
        name: Option<String>,
        pid: Pid,
    ) -> impl Future<Item = bool, Error = Error> {
        name.map_or_else(
            || Either::A(future::ok(false)),
            |name| Either::B(
                self.runtime
                    .list_with_details()
                    .map_err(|e| Error::from(e.context(ErrorKind::ModuleRuntime)))
                    .filter_map(move |(m, rs)| if m.name() == name { Some(rs) } else { None })
                    .into_future()
                    .then(move |result| match result {
                        Ok((Some(rs), _)) => {
                            let authorized = rs.pid() == pid;
                            if !authorized {
                                info!("Request not authorized - expected caller pid: {}, actual caller pid: {}", rs.pid(), pid);
                            }
                            Ok(authorized)
                        },
                        Ok((None, _)) => Ok(false),
                        Err((err, _)) => Err(err),
                    })),
        )
    }

    fn auth_module(
        &self,
        expected_name: &str,
        pid: Pid,
    ) -> impl Future<Item = bool, Error = Error> {
        self.auth_caller(Some(expected_name.to_string()), pid)
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use super::*;
    use error::{Error, ErrorKind};
    use failure::Context;
    use futures::future::FutureResult;
    use futures::stream::Empty;
    use futures::{future, stream};
    use module::{
        LogOptions, Module, ModuleRegistry, ModuleRuntimeState, ModuleSpec,
        SystemInfo as CoreSystemInfo,
    };

    #[test]
    fn should_authorize_anonymous() {
        let runtime = TestModuleList::new(vec![]);
        let auth = Authorization::new(runtime, Policy::Anonymous);
        assert_eq!(true, auth.authorize(None, Pid::None).wait().unwrap());
    }

    #[test]
    fn should_authorize_caller() {
        let runtime = TestModuleList::new(vec![
            TestModule::new("xyz", 987),
            TestModule::new("abc", 123),
        ]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(
            true,
            auth.authorize(Some("abc".to_string()), Pid::Value(123))
                .wait()
                .unwrap()
        );
    }

    #[test]
    fn should_authorize_system_caller() {
        let runtime = TestModuleList::new(vec![
            TestModule::new("xyz", 987),
            TestModule::new("edgeAgent", 123),
        ]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(
            true,
            auth.authorize(Some("$edgeAgent".to_string()), Pid::Value(123))
                .wait()
                .unwrap()
        );
    }

    #[test]
    fn should_reject_caller_without_name() {
        let runtime = TestModuleList::new(vec![TestModule::new("abc", 123)]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(false, auth.authorize(None, Pid::Value(123)).wait().unwrap());
    }

    #[test]
    fn should_reject_caller_with_different_name() {
        let runtime = TestModuleList::new(vec![TestModule::new("abc", 123)]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(
            false,
            auth.authorize(Some("xyz".to_string()), Pid::Value(123))
                .wait()
                .unwrap()
        );
    }

    #[test]
    fn should_reject_caller_with_different_pid() {
        let runtime = TestModuleList::new(vec![TestModule::new("abc", 123)]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(
            false,
            auth.authorize(Some("abc".to_string()), Pid::Value(456))
                .wait()
                .unwrap()
        );
    }

    #[test]
    fn should_authorize_module() {
        let runtime = TestModuleList::new(vec![
            TestModule::new("xyz", 987),
            TestModule::new("abc", 123),
        ]);
        let auth = Authorization::new(runtime, Policy::Module("abc"));
        assert_eq!(true, auth.authorize(None, Pid::Value(123)).wait().unwrap());
    }

    #[test]
    fn should_reject_module_whose_name_does_not_match_policy() {
        let runtime = TestModuleList::new(vec![TestModule::new("xyz", 123)]);
        let auth = Authorization::new(runtime, Policy::Module("abc"));
        assert_eq!(false, auth.authorize(None, Pid::Value(123)).wait().unwrap());
    }

    #[test]
    fn should_reject_module_with_different_pid() {
        let runtime = TestModuleList::new(vec![TestModule::new("abc", 123)]);
        let auth = Authorization::new(runtime, Policy::Module("abc"));
        assert_eq!(false, auth.authorize(None, Pid::Value(456)).wait().unwrap());
    }

    #[test]
    fn should_reject_module_when_runtime_returns_no_pid() {
        let runtime = TestModuleList::new(vec![TestModule::new_with_behavior(
            "abc",
            123,
            TestModuleBehavior::NoPid,
        )]);
        let auth = Authorization::new(runtime, Policy::Caller);
        assert_eq!(
            false,
            auth.authorize(Some("abc".to_string()), Pid::Value(123))
                .wait()
                .unwrap()
        );
    }

    #[test]
    #[should_panic(expected = "A module runtime error occurred.")]
    fn should_fail_when_runtime_state_fails() {
        let runtime = TestModuleList::new(vec![TestModule::new_with_behavior(
            "abc",
            123,
            TestModuleBehavior::FailRuntimeState,
        )]);
        let auth = Authorization::new(runtime, Policy::Caller);
        auth.authorize(Some("abc".to_string()), Pid::Value(123))
            .wait()
            .unwrap();
    }

    #[test]
    #[should_panic(expected = "A module runtime error occurred.")]
    fn should_fail_when_list_fails() {
        let runtime = TestModuleList::new_with_behavior(
            vec![TestModule::new("abc", 123)],
            TestModuleListBehavior::FailList,
        );
        let auth = Authorization::new(runtime, Policy::Caller);
        auth.authorize(Some("abc".to_string()), Pid::Value(123))
            .wait()
            .unwrap();
    }

    struct TestConfig {}

    #[derive(Clone, Copy)]
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

    macro_rules! notimpl_error_stream {
        () => {
            stream::once(Err(Error::new(Context::new(ErrorKind::ModuleRuntime))))
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
                    future::ok(ModuleRuntimeState::default().with_pid(Pid::Value(self.pid)))
                }
                TestModuleBehavior::FailRuntimeState => notimpl_error!(),
                TestModuleBehavior::NoPid => future::ok(ModuleRuntimeState::default()),
            }
        }
    }

    #[derive(Clone, Copy)]
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
        pub fn new(modules: Vec<TestModule>) -> Self {
            TestModuleList {
                modules,
                behavior: TestModuleListBehavior::Default,
            }
        }

        pub fn new_with_behavior(
            modules: Vec<TestModule>,
            behavior: TestModuleListBehavior,
        ) -> Self {
            TestModuleList { modules, behavior }
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
        type Chunk = String;
        type Logs = Empty<Self::Chunk, Self::Error>;

        type CreateFuture = FutureResult<(), Self::Error>;
        type InitFuture = FutureResult<(), Self::Error>;
        type ListFuture = FutureResult<Vec<Self::Module>, Self::Error>;
        type ListWithDetailsStream =
            Box<Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
        type LogsFuture = FutureResult<Self::Logs, Self::Error>;
        type RemoveFuture = FutureResult<(), Self::Error>;
        type RestartFuture = FutureResult<(), Self::Error>;
        type StartFuture = FutureResult<(), Self::Error>;
        type StopFuture = FutureResult<(), Self::Error>;
        type SystemInfoFuture = FutureResult<CoreSystemInfo, Self::Error>;
        type RemoveAllFuture = FutureResult<(), Self::Error>;

        fn init(&self) -> Self::InitFuture {
            notimpl_error!()
        }

        fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
            notimpl_error!()
        }

        fn start(&self, _id: &str) -> Self::StartFuture {
            notimpl_error!()
        }

        fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
            notimpl_error!()
        }

        fn system_info(&self) -> Self::SystemInfoFuture {
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

        fn list_with_details(&self) -> Self::ListWithDetailsStream {
            match self.behavior {
                TestModuleListBehavior::Default => Box::new(stream::futures_unordered(
                    self.modules
                        .clone()
                        .into_iter()
                        .map(|m| m.runtime_state().map(|rs| (m, rs))),
                )),
                TestModuleListBehavior::FailList => Box::new(notimpl_error_stream!()),
            }
        }

        fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
            notimpl_error!()
        }

        fn registry(&self) -> &Self::ModuleRegistry {
            self
        }

        fn remove_all(&self) -> Self::RemoveAllFuture {
            notimpl_error!()
        }
    }
}
