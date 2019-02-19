// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use failure::ResultExt;
use futures::{future, Future};
use hyper::{Body, Request, Response};

use edgelet_core::pid::Pid;
use edgelet_core::{Authorization as CoreAuth, ModuleRuntime, ModuleRuntimeErrorReason, Policy};

use error::{Error, ErrorKind};
use route::{Handler, Parameters};
use IntoResponse;

pub struct Authorization<H, M> {
    auth: CoreAuth<M>,
    inner: Arc<H>,
}

impl<H, M> Authorization<H, M>
where
    H: Handler<Parameters>,
    M: 'static + ModuleRuntime,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
{
    pub fn new(inner: H, policy: Policy, runtime: M) -> Self {
        Authorization {
            auth: CoreAuth::new(runtime, policy),
            inner: Arc::new(inner),
        }
    }
}

impl<H, M> Handler<Parameters> for Authorization<H, M>
where
    H: Handler<Parameters> + Sync,
    M: 'static + ModuleRuntime + Send,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<Future<Item = Response<Body>, Error = Error> + Send> {
        let (name, pid) = (
            params.name("name").map(|n| n.to_string()),
            req.extensions()
                .get::<Pid>()
                .cloned()
                .unwrap_or_else(|| Pid::None),
        );
        let inner = self.inner.clone();

        let response =
            self.auth
                .authorize(name.clone(), pid)
                .then(|authorized| {
                    authorized
                        .context(ErrorKind::Authorization)
                        .map_err(Error::from)
                })
                .and_then(move |authorized| {
                    if authorized {
                        future::Either::A(inner.handle(req, params).then(|resp| {
                            resp.context(ErrorKind::Authorization).map_err(Error::from)
                        }))
                    } else {
                        future::Either::B(future::err(Error::from(ErrorKind::ModuleNotFound(
                            name.unwrap_or_else(String::new),
                        ))))
                    }
                })
                .or_else(|e| future::ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use std::error::Error;
    use std::time::Duration;

    use futures::future::FutureResult;
    use futures::stream::Empty;
    use futures::{future, stream, IntoFuture, Stream};
    use hyper::{Body, Request, Response, StatusCode};

    use edgelet_core::{
        LogOptions, Module, ModuleRegistry, ModuleRuntimeErrorReason, ModuleRuntimeState,
        ModuleSpec, ModuleTop, SystemInfo,
    };

    use super::*;
    use error::Error as HttpError;

    #[test]
    fn handler_calls_inner_handler() {
        let runtime = TestModuleList::new(vec![TestModule::new("abc", 123)]);
        let mut request = Request::default();
        request.extensions_mut().insert(Pid::Value(123));
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "abc".to_string())]);

        let auth = Authorization::new(TestHandler::new(), Policy::Caller, runtime);
        let response = auth.handle(request, params).wait().unwrap();
        let body = response
            .into_body()
            .concat2()
            .and_then(|body| Ok(String::from_utf8(body.to_vec()).unwrap()))
            .wait()
            .unwrap();

        assert_eq!("from TestHandler", body);
    }

    #[test]
    fn handler_responds_with_not_found_when_not_authorized() {
        let runtime = TestModuleList::new(vec![TestModule::new("abc", 123)]);
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "xyz".to_string())]);
        let mut request = Request::default();
        request.extensions_mut().insert(Pid::Value(456));

        let auth = Authorization::new(TestHandler::new(), Policy::Caller, runtime);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[test]
    fn handler_responds_with_not_found_when_name_is_omitted() {
        let runtime = TestModuleList::new(vec![TestModule::new("abc", 123)]);
        let params = Parameters::with_captures(vec![]);
        let mut request = Request::default();
        request.extensions_mut().insert(Pid::Value(123));

        let auth = Authorization::new(TestHandler::new(), Policy::Caller, runtime);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[test]
    fn handler_responds_with_not_found_when_pid_is_omitted() {
        let runtime = TestModuleList::new(vec![TestModule::new("abc", 123)]);
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "abc".to_string())]);
        let mut request = Request::default();
        request.extensions_mut().insert(Pid::None);

        let auth = Authorization::new(TestHandler::new(), Policy::Caller, runtime);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[test]
    fn handler_responds_with_not_found_when_authorizer_fails() {
        let runtime = TestModuleList::new_with_behavior(
            vec![TestModule::new("abc", 123)],
            TestModuleListBehavior::FailCall,
        );
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "abc".to_string())]);
        let mut request = Request::default();
        request.extensions_mut().insert(Pid::None);

        let auth = Authorization::new(TestHandler::new(), Policy::Caller, runtime);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[derive(Debug, Default)]
    struct TestError {
        not_found: bool,
    }

    impl std::fmt::Display for TestError {
        fn fmt(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
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

    #[derive(Clone)]
    struct TestHandler {}

    impl TestHandler {
        pub fn new() -> Self {
            TestHandler {}
        }
    }

    impl Handler<Parameters> for TestHandler {
        fn handle(
            &self,
            _req: Request<Body>,
            _params: Parameters,
        ) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
            let response = Response::builder()
                .status(StatusCode::OK)
                .body("from TestHandler".into())
                .unwrap();
            Box::new(future::ok(response))
        }
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
            Box<Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
        type LogsFuture = FutureResult<Self::Logs, Self::Error>;
        type RemoveFuture = FutureResult<(), Self::Error>;
        type RestartFuture = FutureResult<(), Self::Error>;
        type StartFuture = FutureResult<(), Self::Error>;
        type StopFuture = FutureResult<(), Self::Error>;
        type SystemInfoFuture = FutureResult<SystemInfo, Self::Error>;
        type RemoveAllFuture = FutureResult<(), Self::Error>;
        type TopFuture = FutureResult<ModuleTop, Self::Error>;

        fn init(&self) -> Self::InitFuture {
            notimpl_error!()
        }

        fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
            notimpl_error!()
        }

        fn get(&self, _id: &str) -> Self::GetFuture {
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
                TestModuleListBehavior::FailCall => notimpl_error!(),
            }
        }
    }
}
