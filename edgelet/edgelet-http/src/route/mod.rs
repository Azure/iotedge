// Copyright (c) Microsoft. All rights reserved.

/// This is inspired by [ubnu-intrepid's hyper router](https://github.com/ubnt-intrepid/hyper-router)
/// with some changes to improve usability of the captured parameters
/// when using regex based routes.
use std::clone::Clone;
use std::sync::Arc;

use failure::{Compat, Fail};
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Method, Request, Response, StatusCode};
use url::form_urlencoded::parse as parse_query;

use crate::error::{Error, ErrorKind};
use crate::version::Version;
use crate::IntoResponse;

pub mod macros;
mod regex;

pub type BoxFuture<T, E> = Box<dyn Future<Item = T, Error = E>>;

pub trait Handler<P>: 'static + Send {
    fn handle(
        &self,
        req: Request<Body>,
        params: P,
    ) -> Box<dyn Future<Item = Response<Body>, Error = Error> + Send>;
}

impl<F, P> Handler<P> for F
where
    F: 'static
        + Fn(Request<Body>, P) -> Box<dyn Future<Item = Response<Body>, Error = Error> + Send>
        + Send,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: P,
    ) -> Box<dyn Future<Item = Response<Body>, Error = Error> + Send> {
        (*self)(req, params)
    }
}

pub type HandlerParamsPair<'a, P> = (&'a dyn Handler<P>, P);

pub trait Recognizer {
    type Parameters: 'static;

    fn recognize(
        &self,
        method: &Method,
        version: Version,
        path: &str,
    ) -> Result<HandlerParamsPair<'_, Self::Parameters>, StatusCode>;
}

pub trait Builder: Sized {
    type Recognizer: Recognizer;

    fn route<S, H>(self, method: Method, version: Version, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync;

    fn finish(self) -> Self::Recognizer;

    fn get<S, H>(self, version: Version, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync,
    {
        self.route(Method::GET, version, pattern, handler)
    }

    fn post<S, H>(self, version: Version, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync,
    {
        self.route(Method::POST, version, pattern, handler)
    }

    fn put<S, H>(self, version: Version, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync,
    {
        self.route(Method::PUT, version, pattern, handler)
    }

    fn delete<S, H>(self, version: Version, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync,
    {
        self.route(Method::DELETE, version, pattern, handler)
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
    type InitError = <Self::Service as Service>::Error;

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
    type Error = Compat<Error>;
    type Future = Box<dyn Future<Item = Response<Self::ResBody>, Error = Self::Error> + Send>;

    fn call(&mut self, req: Request<Body>) -> Self::Future {
        let api_version = {
            let query = req.uri().query();
            query.and_then(|query| {
                let mut query = parse_query(query.as_bytes());
                let (_, api_version) = query.find(|&(ref key, _)| key == "api-version")?;

                api_version.parse().ok()
            })
        };

        match api_version {
            Some(api_version) => {
                let method = req.method().clone();
                let path = req.uri().path().to_owned();
                match self.inner.recognize(&method, api_version, &path) {
                    Ok((handler, params)) => {
                        Box::new(handler.handle(req, params).map_err(|err| err.compat()))
                    }
                    Err(code) => Box::new(future::ok(
                        Response::builder()
                            .status(code)
                            .body(Body::empty())
                            .expect("hyper::Response with empty body should not fail to build"),
                    )),
                }
            }
            None => Box::new(future::ok(
                Error::from(ErrorKind::InvalidApiVersion(String::new())).into_response(),
            )),
        }
    }
}

pub use crate::route::regex::{Parameters, RegexRecognizer, RegexRoutesBuilder};
