// Copyright (c) Microsoft. All rights reserved.
#![allow(deprecated)]

use std::io;

use chrono::prelude::*;
use edgelet_core::pid::Pid;
use futures::prelude::*;
use http::{Request, Response};
use http::header::{CONTENT_LENGTH, USER_AGENT};
use hyper::{Body, Error as HyperError};
use hyper::server::{NewService, Service};

#[derive(Clone)]
pub struct LoggingService<T> {
    inner: T,
}

impl<T> LoggingService<T> {
    pub fn new(inner: T) -> LoggingService<T> {
        LoggingService { inner }
    }
}

pub struct ResponseFuture<T> {
    inner: T,
    request: String,
    user_agent: String,
    pid: Option<Pid>,
}

impl<T> Future for ResponseFuture<T>
where
    T: Future<Item = Response<Body>>,
{
    type Item = T::Item;
    type Error = T::Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        let response = try_ready!(self.inner.poll());

        let body_length = response
            .headers()
            .get(CONTENT_LENGTH)
            .and_then(|l| l.to_str().ok().map(|l| l.to_string()))
            .unwrap_or_else(|| "-".to_string());
        let pid = self.pid
            .as_ref()
            .map(|p| p.to_string())
            .unwrap_or_else(|| "-".to_string());

        info!(
            "- - - [{}] \"{}\" {} {} \"-\" \"{}\" pid({})",
            Utc::now(),
            self.request,
            response.status(),
            body_length,
            self.user_agent,
            pid,
        );
        Ok(Async::Ready(response))
    }
}

impl<T> Service for LoggingService<T>
where
    T: Service<Request = Request<Body>, Response = Response<Body>>,
{
    type Request = T::Request;
    type Response = T::Response;
    type Error = T::Error;
    type Future = ResponseFuture<T::Future>;

    fn call(&self, req: Self::Request) -> Self::Future {
        let request = format!("{} {} {:?}", req.method(), req.uri().path(), req.version());
        let user_agent = req.headers()
            .get(USER_AGENT)
            .and_then(|ua| ua.to_str().ok())
            .unwrap_or_else(|| "-")
            .to_string();
        let pid = req.extensions().get::<Pid>().cloned();

        let inner = self.inner.call(req);
        ResponseFuture {
            inner,
            request,
            user_agent,
            pid,
        }
    }
}

impl<T> NewService for LoggingService<T>
where
    T: Clone + Service<Request = Request<Body>, Response = Response<Body>, Error = HyperError>,
    T::Future: 'static,
{
    type Request = T::Request;
    type Response = Response<Body>;
    type Error = HyperError;
    type Instance = Self;

    fn new_service(&self) -> io::Result<Self::Instance> {
        Ok(self.clone())
    }
}
