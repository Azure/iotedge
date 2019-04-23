// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

use edgelet_core::{AuthId, Authenticator};
use failure::Fail;
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Request};

use crate::{Error, ErrorKind, IntoResponse};

#[derive(Clone)]
pub struct AuthenticationService<M, S> {
    runtime: M,
    inner: S,
}

impl<M, S> AuthenticationService<M, S> {
    pub fn new(runtime: M, inner: S) -> Self {
        AuthenticationService { runtime, inner }
    }
}

impl<M, S> Service for AuthenticationService<M, S>
where
    M: Authenticator<Request = Request<S::ReqBody>> + Send + Clone + 'static,
    M::AuthenticateFuture: Future<Item = AuthId> + Send + 'static,
    <M::AuthenticateFuture as Future>::Error: Fail,
    S: Service<ReqBody = Body, ResBody = Body> + Send + Clone + 'static,
    S::Future: Send + 'static,
    S::Error: Send,
{
    type ReqBody = S::ReqBody;
    type ResBody = S::ResBody;
    type Error = S::Error;
    type Future = Box<
        dyn Future<Item = <S::Future as Future>::Item, Error = <S::Future as Future>::Error> + Send,
    >;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let mut req = req;
        let mut inner = self.inner.clone();
        Box::new(
            self.runtime
                .authenticate(&req)
                .then(move |auth_id| match auth_id {
                    Ok(auth_id) => {
                        req.extensions_mut().insert(auth_id);
                        future::Either::A(inner.call(req))
                    }

                    Err(err) => future::Either::B(future::ok(
                        Error::from(err.context(ErrorKind::Authorization)).into_response(),
                    )),
                }),
        )
    }
}

impl<M, S> NewService for AuthenticationService<M, S>
where
    M: Authenticator + Send + Clone + 'static,
    S: NewService,
    S::Future: Send + 'static,
    AuthenticationService<M, <S as NewService>::Service>: Service,
{
    type ReqBody = <AuthenticationService<M, <S as NewService>::Service> as Service>::ReqBody;
    type ResBody = <AuthenticationService<M, <S as NewService>::Service> as Service>::ResBody;
    type Error = <AuthenticationService<M, <S as NewService>::Service> as Service>::Error;
    type Service = AuthenticationService<M, <S as NewService>::Service>;
    type Future = Box<dyn Future<Item = Self::Service, Error = Self::InitError> + Send>;
    type InitError = <S as NewService>::InitError;

    fn new_service(&self) -> Self::Future {
        let runtime = self.runtime.clone();
        Box::new(
            self.inner
                .new_service()
                .map(|inner| AuthenticationService::new(runtime, inner)),
        )
    }
}

#[cfg(test)]
mod tests {
    use crate::authentication::AuthenticationService;
    use edgelet_core::{AuthId, Authenticator, Error, ErrorKind};
    use futures::{future, Future, Stream};
    use hyper::service::Service;
    use hyper::{Body, Request, Response, StatusCode};
    use tokio::io;

    #[test]
    fn service_calls_inner_service_when_module_authenticated() {
        let req = Request::default();
        let runtime = TestAuthenticator::authenticated(Some("abc".to_string()));
        let mut auth = AuthenticationService::new(runtime, TestService);

        let response = auth.call(req).wait().unwrap();
        let body = response
            .into_body()
            .concat2()
            .and_then(|body| Ok(String::from_utf8(body.to_vec()).unwrap()))
            .wait()
            .unwrap();

        assert_eq!("from TestService", body);
    }

    #[test]
    fn service_calls_inner_service_when_any_authenticated() {
        let req = Request::default();
        let runtime = TestAuthenticator::authenticated(None);
        let mut auth = AuthenticationService::new(runtime, TestService);

        let response = auth.call(req).wait().unwrap();
        let body = response
            .into_body()
            .concat2()
            .and_then(|body| Ok(String::from_utf8(body.to_vec()).unwrap()))
            .wait()
            .unwrap();

        assert_eq!("from TestService", body);
    }

    #[test]
    fn service_responds_with_not_found_when_not_authenticated() {
        let runtime = TestAuthenticator::not_authenticated();
        let req = Request::default();
        let inner = TestService;
        let mut auth = AuthenticationService::new(runtime, inner);

        let response = auth.call(req).wait().unwrap();

        assert_eq!(404, response.status());
    }

    #[test]
    fn service_responds_with_not_found_when_error() {
        let runtime = TestAuthenticator::error();
        let req = Request::default();
        let inner = TestService;
        let mut auth = AuthenticationService::new(runtime, inner);

        let response = auth.call(req).wait().unwrap();

        assert_eq!(404, response.status());
    }

    #[derive(Clone)]
    struct TestAuthenticator {
        auth: Option<AuthId>,
        error: Option<String>,
    }

    impl TestAuthenticator {
        fn authenticated(name: Option<String>) -> Self {
            let auth = match name {
                Some(name) => AuthId::Value(name),
                None => AuthId::Any,
            };
            TestAuthenticator {
                auth: Some(auth),
                error: None,
            }
        }

        fn not_authenticated() -> Self {
            TestAuthenticator {
                auth: Some(AuthId::None),
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
            let fut = match auth {
                Some(auth_id) => future::ok(auth_id),
                None => {future::err(Error::from(ErrorKind::ModuleRuntime))},
            };

            Box::new(fut)
        }
    }

    #[derive(Clone)]
    struct TestService;

    impl Service for TestService {
        type ReqBody = Body;
        type ResBody = Body;
        type Error = io::Error;
        type Future = Box<dyn Future<Item = Response<Body>, Error = Self::Error> + Send>;

        fn call(&mut self, _req: Request<Self::ReqBody>) -> Self::Future {
            let response = Response::builder()
                .status(StatusCode::OK)
                .body("from TestService".into())
                .unwrap();
            Box::new(future::ok(response))
        }
    }
}
