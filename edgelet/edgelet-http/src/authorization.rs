// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{pid::Pid, Authorization as CoreAuth, Error as CoreError, Module, ModuleRuntime,
                   Policy};
use error::{Error, ErrorKind};
use futures::{future, future::Either, Future};
use http::{Request, Response};
use hyper::{Body, Error as HyperError};
use route::{BoxFuture, Handler, Parameters};
use std::rc::Rc;
use IntoResponse;

pub struct Authorization<H, M>
where
    H: Handler<Parameters>,
    M: 'static + ModuleRuntime,
{
    auth: CoreAuth<M>,
    inner: Rc<H>,
}

impl<H, M> Authorization<H, M>
where
    H: Handler<Parameters>,
    M: 'static + ModuleRuntime,
    M::Error: Into<CoreError>,
    <M::Module as Module>::Error: Into<CoreError>,
{
    pub fn new(inner: H, policy: Policy, runtime: M) -> Self {
        Authorization {
            auth: CoreAuth::new(runtime, policy),
            inner: Rc::new(inner),
        }
    }
}

impl<H, M> Handler<Parameters> for Authorization<H, M>
where
    H: Handler<Parameters>,
    M: 'static + ModuleRuntime,
    M::Error: Into<CoreError>,
    <M::Module as Module>::Error: Into<CoreError>,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        let (name, pid) = (
            params.name("name").map(|n| n.to_string()),
            req.extensions()
                .get::<Pid>()
                .cloned()
                .unwrap_or_else(|| Pid::None),
        );
        let inner = self.inner.clone();

        let response = self.auth
            .authorize(name, pid)
            .map_err(Error::from)
            .and_then(move |authorized| {
                if authorized {
                    Either::A(inner.handle(req, params).map_err(Error::from))
                } else {
                    Either::B(future::err(Error::from(ErrorKind::NotFound)))
                }
            })
            .or_else(|e| future::ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use edgelet_core::{LogOptions, ModuleRegistry, ModuleRuntimeState, ModuleSpec};
    use futures::{future::FutureResult, stream::Empty, Stream};
    use http::{Request, Response, StatusCode};
    use hyper::{Body, Error as HyperError};

    #[test]
    fn handler_calls_inner_handler() {
        let runtime = TestModuleList::new(&vec![TestModule::new("abc", 123)]);
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
        let runtime = TestModuleList::new(&vec![TestModule::new("abc", 123)]);
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "xyz".to_string())]);
        let mut request = Request::default();
        request.extensions_mut().insert(Pid::Value(456));

        let auth = Authorization::new(TestHandler::new(), Policy::Caller, runtime);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[test]
    fn handler_responds_with_not_found_when_name_is_omitted() {
        let runtime = TestModuleList::new(&vec![TestModule::new("abc", 123)]);
        let params = Parameters::with_captures(vec![]);
        let mut request = Request::default();
        request.extensions_mut().insert(Pid::Value(123));

        let auth = Authorization::new(TestHandler::new(), Policy::Caller, runtime);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[test]
    fn handler_responds_with_not_found_when_pid_is_omitted() {
        let runtime = TestModuleList::new(&vec![TestModule::new("abc", 123)]);
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "abc".to_string())]);
        let mut request = Request::default();
        request.extensions_mut().insert(Pid::None);

        let auth = Authorization::new(TestHandler::new(), Policy::Caller, runtime);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[test]
    fn handler_responds_with_not_found_when_authorizer_fails() {
        let runtime = TestModuleList::new(&vec![TestModule::new_with_behavior(
            "abc",
            123,
            TestModuleBehavior::FailRuntimeState,
        )]);
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "abc".to_string())]);
        let mut request = Request::default();
        request.extensions_mut().insert(Pid::None);

        let auth = Authorization::new(TestHandler::new(), Policy::Caller, runtime);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
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
        ) -> BoxFuture<Response<Body>, HyperError> {
            let response = Response::builder()
                .status(StatusCode::OK)
                .body("from TestHandler".into())
                .unwrap();
            Box::new(future::ok(response))
        }
    }

    struct TestConfig {}

    #[derive(Clone)]
    enum TestModuleBehavior {
        Default,
        FailRuntimeState,
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
            future::err(Error::from(ErrorKind::InvalidApiVersion))
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
                    future::ok(ModuleRuntimeState::default().with_pid(&Pid::Value(self.pid)))
                }
                TestModuleBehavior::FailRuntimeState => notimpl_error!(),
            }
        }
    }

    #[derive(Clone)]
    struct TestModuleList {
        modules: Vec<TestModule>,
    }

    impl TestModuleList {
        pub fn new(modules: &Vec<TestModule>) -> Self {
            TestModuleList {
                modules: modules.clone(),
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
        type Chunk = String;
        type Logs = Empty<Self::Chunk, Self::Error>;
        type CreateFuture = FutureResult<(), Self::Error>;
        type InitFuture = FutureResult<(), Self::Error>;
        type ListFuture = FutureResult<Vec<Self::Module>, Self::Error>;
        type LogsFuture = FutureResult<Self::Logs, Self::Error>;
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
            future::ok(self.modules.clone())
        }

        fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
            notimpl_error!()
        }

        fn registry(&self) -> &Self::ModuleRegistry {
            self
        }
    }
}
