// Copyright (c) Microsoft. All rights reserved.

/// This is inspired by [ubnu-intrepid's hyper router](https://github.com/ubnt-intrepid/hyper-router)
/// with some changes to improve usability of the captured parameters
/// when using regex based routes.
use std::clone::Clone;
use std::error::Error as StdError;
use std::sync::Arc;

use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{self, Body, Method, Request, Response, StatusCode};

pub mod macros;
mod regex;

pub type BoxFuture<T, E> = Box<Future<Item = T, Error = E>>;

pub trait Handler<P>: 'static + Send {
    fn handle(
        &self,
        req: Request<Body>,
        params: P,
    ) -> Box<Future<Item = Response<Body>, Error = hyper::Error> + Send>;
}

impl<F, P> Handler<P> for F
where
    F: 'static
        + Fn(Request<Body>, P) -> Box<Future<Item = Response<Body>, Error = hyper::Error> + Send>
        + Send,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: P,
    ) -> Box<Future<Item = Response<Body>, Error = hyper::Error> + Send> {
        (*self)(req, params)
    }
}

pub type HandlerParamsPair<'a, P> = (&'a Handler<P>, P);

pub trait Recognizer {
    type Parameters: 'static;

    fn recognize(
        &self,
        method: &Method,
        path: &str,
    ) -> Result<HandlerParamsPair<Self::Parameters>, StatusCode>;
}

pub trait Builder: Sized {
    type Recognizer: Recognizer;

    fn route<S, H>(self, method: Method, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync;

    fn finish(self) -> Self::Recognizer;

    fn get<S, H>(self, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync,
    {
        self.route(Method::GET, pattern, handler)
    }

    fn post<S, H>(self, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync,
    {
        self.route(Method::POST, pattern, handler)
    }

    fn put<S, H>(self, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync,
    {
        self.route(Method::PUT, pattern, handler)
    }

    fn delete<S, H>(self, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync,
    {
        self.route(Method::DELETE, pattern, handler)
    }
}

pub struct Router<R: Recognizer> {
    inner: Arc<R>,
}

impl<R: Recognizer> From<R> for Router<R> {
    fn from(recognizer: R) -> Self {
        Router {
            inner: Arc::new(recognizer),
        }
    }
}

impl<R> NewService for Router<R>
where
    R: Recognizer,
{
    type ReqBody = <Self::Service as Service>::ReqBody;
    type ResBody = <Self::Service as Service>::ResBody;
    type Error = <Self::Service as Service>::Error;
    type Service = RouterService<R>;
    type Future = future::FutureResult<Self::Service, Self::InitError>;
    type InitError = Box<StdError + Send + Sync>;

    fn new_service(&self) -> Self::Future {
        future::ok(RouterService {
            inner: self.inner.clone(),
        })
    }
}

pub struct RouterService<R: Recognizer> {
    inner: Arc<R>,
}

impl<R> Clone for RouterService<R>
where
    R: Recognizer,
{
    fn clone(&self) -> Self {
        RouterService {
            inner: self.inner.clone(),
        }
    }
}

impl<R> Service for RouterService<R>
where
    R: Recognizer,
{
    type ReqBody = Body;
    type ResBody = Body;
    type Error = hyper::Error;
    type Future = Box<Future<Item = Response<Self::ResBody>, Error = Self::Error> + Send>;

    fn call(&mut self, req: Request<Body>) -> Self::Future {
        let method = req.method().clone();
        let path = req.uri().path().to_owned();
        match self.inner.recognize(&method, &path) {
            Ok((handler, params)) => handler.handle(req, params),

            Err(code) => Box::new(future::ok(
                Response::builder()
                    .status(code)
                    .body(Body::empty())
                    .expect("hyper::Response with empty body should not fail to build"),
            )),
        }
    }
}

pub use route::regex::{Parameters, RegexRecognizer, RegexRoutesBuilder};
