// Copyright (c) Microsoft. All rights reserved.
#![allow(deprecated)]

use std::io;

use chrono::prelude::*;
use futures::prelude::*;
use hyper::Error as HyperError;
use hyper::header::{ContentLength, UserAgent};
use hyper::server::{NewService, Request, Response, Service};

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
}

impl<T> Future for ResponseFuture<T>
where
    T: Future<Item = Response>,
{
    type Item = T::Item;
    type Error = T::Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        let response = try_ready!(self.inner.poll());

        let body_length = response
            .headers()
            .get::<ContentLength>()
            .map(|l| l.to_string())
            .unwrap_or_else(|| "-".to_string());

        info!(
            "- - - [{}] \"{}\" {} {} \"-\" \"{}\"",
            Utc::now(),
            self.request,
            response.status(),
            body_length,
            self.user_agent,
        );
        Ok(Async::Ready(response))
    }
}

impl<T> Service for LoggingService<T>
where
    T: Service<Request = Request, Response = Response>,
{
    type Request = T::Request;
    type Response = T::Response;
    type Error = T::Error;
    type Future = ResponseFuture<T::Future>;

    fn call(&self, req: Self::Request) -> Self::Future {
        let request = format!("{} {} {}", req.method(), req.uri().path(), req.version());
        let user_agent = req.headers()
            .get::<UserAgent>()
            .map(|ua| ua.to_string())
            .unwrap_or_else(|| "-".to_string());

        let inner = self.inner.call(req);
        ResponseFuture {
            inner,
            request,
            user_agent,
        }
    }
}

impl<T> NewService for LoggingService<T>
where
    T: Clone + Service<Request = Request, Response = Response, Error = HyperError>,
    T::Future: 'static,
{
    type Request = T::Request;
    type Response = Response;
    type Error = HyperError;
    type Instance = Self;

    fn new_service(&self) -> io::Result<Self::Instance> {
        Ok(self.clone())
    }
}
