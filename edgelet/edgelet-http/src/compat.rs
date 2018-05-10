// Copyright (c) Microsoft. All rights reserved.

//! Wrappers to build compatibility with the `http` crate.

use futures::{Future, Poll};
use http;
use hyper::server::Service;
use hyper::{Body, Error, Request, Response};

/// Wraps a `Future` returning an `http::Response` into
/// a `Future` returning a `hyper::server::Response`.
#[derive(Debug)]
pub(crate) struct CompatFuture<F> {
    future: F,
}

impl<F, Bd> Future for CompatFuture<F>
where
    F: Future<Item = http::Response<Bd>, Error = Error>,
{
    type Item = Response<Bd>;
    type Error = Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        self.future.poll().map(|a| a.map(|res| res.into()))
    }
}

/// Wraps a `Service` taking an `http::Request` and returning
/// an `http::Response` into a `Service` taking a `hyper::server::Request`,
/// and returning a `hyper::server::Response`.
#[derive(Debug)]
pub(crate) struct CompatService<S> {
    service: S,
}

pub(crate) fn service<S>(service: S) -> CompatService<S> {
    CompatService { service }
}

impl<S, Bd> Service for CompatService<S>
where
    S: Service<Request = http::Request<Body>, Response = http::Response<Bd>, Error = Error>,
{
    type Request = Request;
    type Response = Response<Bd>;
    type Error = Error;
    type Future = CompatFuture<S::Future>;

    fn call(&self, req: Self::Request) -> Self::Future {
        CompatFuture {
            future: self.service.call(req.into()),
        }
    }
}
