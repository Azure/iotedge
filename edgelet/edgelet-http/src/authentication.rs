// Copyright (c) Microsoft. All rights reserved.
use std::sync::Arc;

use failure::Fail;
use futures::future::Either;
use futures::{future, Future};
use hyper::{Body, Request, Response};

use edgelet_core::{AuthId, Authenticator, ModuleId, Policy};

use crate::route::{Handler, Parameters};
use crate::{Error, ErrorKind, IntoResponse};

pub struct Authentication<H, M> {
    policy: Policy,
    runtime: M,
    inner: Arc<H>,
}

impl<H, M> Authentication<H, M> {
    pub fn new(inner: H, policy: Policy, runtime: M) -> Self {
        Authentication {
            policy,
            runtime,
            inner: Arc::new(inner),
        }
    }
}

impl<H, M> Handler<Parameters> for Authentication<H, M>
where
    H: Handler<Parameters> + Sync,
    M: Authenticator<Request = Request<Body>> + Send + 'static,
    <M::AuthenticateFuture as Future>::Error: Fail,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = Error> + Send> {
        let mut req = req;

        let name = params.name("name");

        let authenticate = match self.policy.should_authenticate(name) {
            (true, name) => {
                if let Some(name) = name {
                    req.extensions_mut().insert(ModuleId::from(name));
                }
                Either::A(self.runtime.authenticate(&req))
            }
            (false, _) => Either::B(future::ok(AuthId::Any)),
        };

        let inner = self.inner.clone();

        let response = authenticate.then(move |auth_id| match auth_id {
            Ok(auth_id) => {
                req.extensions_mut().insert(auth_id);
                future::Either::A(inner.handle(req, params))
            }
            Err(err) => future::Either::B(future::ok(
                Error::from(err.context(ErrorKind::Authorization)).into_response(),
            )),
        });

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use futures::{future, Future, Stream};
    use hyper::{Body, Request, Response, StatusCode};

    use edgelet_core::{AuthId, Authenticator, Error, ErrorKind, Policy};

    use crate::authentication::Authentication;
    use crate::error::Error as HttpError;
    use crate::route::{Handler, Parameters};

    #[test]
    fn handler_calls_inner_with_auth_any_when_policy_anonymous() {
        let auth_id = AuthId::Value("abc".into());
        let policy = Policy::Anonymous;
        let req = Request::default();
        let runtime = TestAuthenticator::authenticated(auth_id);
        let inner = TestHandler::new();
        let auth = Authentication::new(inner, policy, runtime);

        let response = auth.handle(req, Parameters::new()).wait().unwrap();

        let body = response
            .into_body()
            .concat2()
            .and_then(|body| Ok(String::from_utf8(body.to_vec()).unwrap()))
            .wait()
            .unwrap();
        assert_eq!("auth = any", body);
    }

    #[test]
    fn handler_calls_inner_with_auth_any_when_caller_authenticated() {
        let auth_id = AuthId::Value("abc".into());
        let policy = Policy::Caller;
        let req = Request::default();
        let runtime = TestAuthenticator::authenticated(auth_id);
        let inner = TestHandler::new();
        let auth = Authentication::new(inner, policy, runtime);

        let response = auth.handle(req, Parameters::new()).wait().unwrap();

        let body = response
            .into_body()
            .concat2()
            .and_then(|body| Ok(String::from_utf8(body.to_vec()).unwrap()))
            .wait()
            .unwrap();
        assert_eq!("auth = abc", body);
    }

    #[test]
    fn handler_calls_inner_with_auth_none_when_caller_not_authenticated() {
        let auth_id = AuthId::None;
        let policy = Policy::Module("xyz");
        let req = Request::default();
        let runtime = TestAuthenticator::authenticated(auth_id);
        let inner = TestHandler::new();
        let auth = Authentication::new(inner, policy, runtime);

        let response = auth.handle(req, Parameters::new()).wait().unwrap();

        let body = response
            .into_body()
            .concat2()
            .and_then(|body| Ok(String::from_utf8(body.to_vec()).unwrap()))
            .wait()
            .unwrap();
        assert_eq!("auth = none", body);
    }

    #[test]
    fn handler_responds_with_not_found_when_error() {
        let policy = Policy::Caller;
        let req = Request::default();
        let runtime = TestAuthenticator::error();
        let inner = TestHandler::new();
        let auth = Authentication::new(inner, policy, runtime);

        let response = auth.handle(req, Parameters::new()).wait().unwrap();

        assert_eq!(404, response.status());
    }

    #[derive(Clone)]
    struct TestAuthenticator {
        auth: Option<AuthId>,
        error: Option<String>,
    }

    impl TestAuthenticator {
        fn authenticated(auth_id: AuthId) -> Self {
            TestAuthenticator {
                auth: Some(auth_id),
                error: None,
            }
        }

        fn error() -> Self {
            TestAuthenticator {
                auth: None,
                error: Some("unexpected error".to_string()),
            }
        }
    }

    impl Authenticator for TestAuthenticator {
        type Error = Error;
        type Request = Request<Body>;
        type AuthenticateFuture = Box<dyn Future<Item = AuthId, Error = Self::Error> + Send>;

        fn authenticate(&self, _req: &Self::Request) -> Self::AuthenticateFuture {
            let auth = self.auth.clone();

            Box::new(match auth {
                Some(auth_id) => future::ok(auth_id),
                None => future::err(Error::from(ErrorKind::ModuleRuntime)),
            })
        }
    }

    #[derive(Clone)]
    struct TestHandler;

    impl TestHandler {
        pub fn new() -> Self {
            TestHandler {}
        }
    }

    impl Handler<Parameters> for TestHandler {
        fn handle(
            &self,
            req: Request<Body>,
            _params: Parameters,
        ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
            let body = req
                .extensions()
                .get::<AuthId>()
                .map_or_else(
                    || "AuthId expected".to_string(),
                    |auth_id| format!("auth = {}", auth_id),
                )
                .into();

            let response = Response::builder()
                .status(StatusCode::OK)
                .body(body)
                .unwrap();
            Box::new(future::ok(response))
        }
    }
}
