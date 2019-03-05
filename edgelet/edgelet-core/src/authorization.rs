// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;
use futures::future::Either;
use futures::{future, Future};
use log::info;

use crate::error::{Error, ErrorKind};
use crate::module::{ModuleRuntime, ModuleRuntimeErrorReason};
use crate::pid::Pid;

#[derive(Debug)]
pub enum Policy {
    Anonymous,
    Caller,
    Module(&'static str),
}

pub struct Authorization<M> {
    runtime: M,
    policy: Policy,
}

impl<M> Authorization<M>
where
    M: 'static + ModuleRuntime,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
{
    pub fn new(runtime: M, policy: Policy) -> Self {
        Authorization { runtime, policy }
    }

    pub fn authorize(
        &self,
        name: Option<String>,
        pid: Pid,
    ) -> impl Future<Item = bool, Error = Error> {
        let name = name.map(|n| n.trim_start_matches('$').to_string());
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
            |name| {
                Either::B(self.runtime.top(&name).then(move |result| match result {
                    Ok(mt) => {
                        let authorize = mt.process_ids().contains(&pid);
                        if !authorize {
                            info!(
                                "Request not authorized - caller pid {} not found in module {}",
                                pid, name
                            );
                        }
                        Ok(authorize)
                    }
                    Err(err) => match (&err).into() {
                        ModuleRuntimeErrorReason::NotFound => {
                            info!("Request not authorized - module {} not found", name);
                            Ok(false)
                        }
                        _ => Err(Error::from(err.context(ErrorKind::ModuleRuntime))),
                    },
                }))
            },
        )
    }

    fn auth_module(
        &self,
        expected_name: &'static str,
        pid: Pid,
    ) -> impl Future<Item = bool, Error = Error> {
        self.runtime
            .get(expected_name)
            .then(move |m| match m {
                Ok((_, rs)) => match rs.pid() {
                    p if p == pid => Ok(true),
                    _ => {
                        info!("Request not authorized - expected caller pid: {}, actual caller pid: {}", rs.pid(), pid);
                        Ok(false)
                    },
                },
                Err(err) => match (&err).into() {
                    ModuleRuntimeErrorReason::NotFound => {
                        info!("Request not authorized - module {} not found", expected_name);
                        Ok(false)
                    },
                    _ => Err(Error::from(err.context(ErrorKind::ModuleRuntime))),
                },
            })
    }
}

#[cfg(test)]
mod tests {
    use futures::future::FutureResult;
    use futures::stream::Empty;
    use futures::{future, stream, IntoFuture, Stream};
    use std::error::Error;
    use std::time::Duration;

    use super::*;
    use crate::module::{
        LogOptions, Module, ModuleRegistry, ModuleRuntimeState, ModuleSpec, ModuleTop,
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
    #[should_panic(expected = "A module runtime error occurred.")]
    fn should_fail_when_top_fails() {
        let runtime = TestModuleList::new_with_behavior(
            vec![TestModule::new("abc", 123)],
            TestModuleListBehavior::FailCall,
        );
        let auth = Authorization::new(runtime, Policy::Caller);
        auth.authorize(Some("abc".to_string()), Pid::Value(123))
            .wait()
            .unwrap();
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
        let runtime = TestModuleList::new_with_behavior(
            vec![TestModule::new("abc", 123)],
            TestModuleListBehavior::NoPid,
        );
        let auth = Authorization::new(runtime, Policy::Module("abc"));
        assert_eq!(false, auth.authorize(None, Pid::Value(123)).wait().unwrap());
    }

    #[test]
    #[should_panic(expected = "A module runtime error occurred.")]
    fn should_fail_when_get_fails() {
        let runtime = TestModuleList::new_with_behavior(
            vec![TestModule::new("abc", 123)],
            TestModuleListBehavior::FailCall,
        );
        let auth = Authorization::new(runtime, Policy::Module("abc"));
        auth.authorize(Some("abc".to_string()), Pid::Value(123))
            .wait()
            .unwrap();
    }

    #[derive(Debug, Default)]
    struct TestError {
        not_found: bool,
    }

    impl std::fmt::Display for TestError {
        fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
            write!(f, "TestError")
        }
    }

    impl TestError {
        pub fn new() -> Self {
            TestError { not_found: false }
        }
        pub fn new_not_found() -> Self {
            TestError { not_found: true }
        }
    }

    impl Error for TestError {
        fn description(&self) -> &str {
            "A test error occurred."
        }
    }

    impl<'a> From<&'a TestError> for ModuleRuntimeErrorReason {
        fn from(err: &'a TestError) -> Self {
            if err.not_found {
                ModuleRuntimeErrorReason::NotFound
            } else {
                ModuleRuntimeErrorReason::Other
            }
        }
    }

    macro_rules! notimpl_error {
        () => {
            future::err(TestError::new())
        };
    }

    macro_rules! notimpl_error_stream {
        () => {
            stream::once(Err(TestError::new()))
        };
    }

    struct TestConfig {}

    #[derive(Clone)]
    struct TestModule {
        name: String,
        pid: i32,
    }

    impl TestModule {
        pub fn new(name: &str, pid: i32) -> Self {
            let name = name.to_string();
            TestModule { name, pid }
        }
    }

    impl Module for TestModule {
        type Config = TestConfig;
        type Error = TestError;
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
            notimpl_error!()
        }
    }

    #[derive(Clone, Copy)]
    enum TestModuleListBehavior {
        Default,
        FailCall,
        NoPid,
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
        type Error = TestError;
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
        type Error = TestError;
        type Config = TestConfig;
        type Module = TestModule;
        type ModuleRegistry = Self;
        type Chunk = String;
        type Logs = Empty<Self::Chunk, Self::Error>;

        type CreateFuture = FutureResult<(), Self::Error>;
        type GetFuture = FutureResult<(Self::Module, ModuleRuntimeState), Self::Error>;
        type InitFuture = FutureResult<(), Self::Error>;
        type ListFuture = FutureResult<Vec<Self::Module>, Self::Error>;
        type ListWithDetailsStream =
            Box<dyn Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
        type LogsFuture = FutureResult<Self::Logs, Self::Error>;
        type RemoveFuture = FutureResult<(), Self::Error>;
        type RestartFuture = FutureResult<(), Self::Error>;
        type StartFuture = FutureResult<(), Self::Error>;
        type StopFuture = FutureResult<(), Self::Error>;
        type SystemInfoFuture = FutureResult<CoreSystemInfo, Self::Error>;
        type RemoveAllFuture = FutureResult<(), Self::Error>;
        type TopFuture = FutureResult<ModuleTop, Self::Error>;

        fn init(&self) -> Self::InitFuture {
            notimpl_error!()
        }

        fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
            notimpl_error!()
        }

        fn get(&self, id: &str) -> Self::GetFuture {
            let module = self
                .modules
                .iter()
                .find(|&m| m.name == id)
                .ok_or_else(TestError::new_not_found);
            match self.behavior {
                TestModuleListBehavior::Default => module
                    .map(|m| {
                        (
                            m.clone(),
                            ModuleRuntimeState::default().with_pid(Pid::Value(m.pid)),
                        )
                    })
                    .into_future(),
                TestModuleListBehavior::NoPid => module
                    .map(|m| (m.clone(), ModuleRuntimeState::default()))
                    .into_future(),
                TestModuleListBehavior::FailCall => notimpl_error!(),
            }
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
            notimpl_error!()
        }

        fn list_with_details(&self) -> Self::ListWithDetailsStream {
            Box::new(notimpl_error_stream!())
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

        fn top(&self, id: &str) -> Self::TopFuture {
            let module = self
                .modules
                .iter()
                .find(|&m| m.name == id)
                .ok_or_else(TestError::new_not_found);
            match self.behavior {
                TestModuleListBehavior::Default => module
                    .map(|m| ModuleTop::new(m.name.clone(), vec![Pid::Value(m.pid)]))
                    .into_future(),
                TestModuleListBehavior::NoPid => unimplemented!(),
                TestModuleListBehavior::FailCall => notimpl_error!(),
            }
        }
    }
}
