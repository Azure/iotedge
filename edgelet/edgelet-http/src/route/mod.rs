// Copyright (c) Microsoft. All rights reserved.

/// This is inspired by ubnu-intrepid's hyper router (https://github.com/ubnt-intrepid/hyper-router)
/// with some changes to improve usability of the captured parameters
/// when using regex based routes.

use std::clone::Clone;
use std::io;
use std::sync::Arc;

use futures::{future, Future};
use http::{Method, Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use hyper::server::{NewService, Service};

pub mod macros;
mod regex;

pub type BoxFuture<T, E> = Box<Future<Item = T, Error = E>>;

pub trait Handler<P>: 'static {
    fn handle(&self, req: Request<Body>, params: P) -> BoxFuture<Response<Body>, HyperError>;
}

impl<F, P> Handler<P> for F
where
    F: 'static + Fn(Request<Body>, P) -> BoxFuture<Response<Body>, HyperError>,
{
    fn handle(&self, req: Request<Body>, params: P) -> BoxFuture<Response<Body>, HyperError> {
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
        H: Handler<<Self::Recognizer as Recognizer>::Parameters>;

    fn finish(self) -> Self::Recognizer;

    fn get<S, H>(self, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters>,
    {
        self.route(Method::GET, pattern, handler)
    }

    fn post<S, H>(self, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters>,
    {
        self.route(Method::POST, pattern, handler)
    }

    fn put<S, H>(self, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters>,
    {
        self.route(Method::PUT, pattern, handler)
    }

    fn delete<S, H>(self, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters>,
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
    type Request = Request<Body>;
    type Response = Response<Body>;
    type Error = HyperError;
    type Instance = RouterService<R>;

    fn new_service(&self) -> io::Result<Self::Instance> {
        Ok(RouterService {
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
    type Request = Request<Body>;
    type Response = Response<Body>;
    type Error = HyperError;
    type Future = BoxFuture<Self::Response, HyperError>;

    fn call(&self, req: Request<Body>) -> Self::Future {
        let method = req.method().clone();
        let path = req.uri().path().to_owned();
        self.inner
            .recognize(&method, &path)
            .map(|(handler, params)| handler.handle(req, params))
            .unwrap_or_else(|code| {
                Box::new(future::result(
                    Response::builder()
                        .status(code)
                        .body(Body::default())
                        .map_err(|_| HyperError::Status),
                ))
            })
    }
}

pub use route::regex::{Parameters, RegexRecognizer, RegexRoutesBuilder};
