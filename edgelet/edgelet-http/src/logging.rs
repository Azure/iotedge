// Copyright (c) Microsoft. All rights reserved.
#![allow(deprecated)]

use chrono::prelude::*;
use edgelet_core::pid::Pid;
use futures::prelude::*;
use hyper::header::{CONTENT_LENGTH, USER_AGENT};
use hyper::service::{NewService, Service};
use hyper::Request;
use log::info;

#[derive(Clone)]
pub struct LoggingService<T> {
    label: String,
    inner: T,
}

impl<T> LoggingService<T> {
    pub fn new(label: String, inner: T) -> Self {
        LoggingService { label, inner }
    }
}

impl<T> Service for LoggingService<T>
where
    T: Service,
    <T as Service>::Future: Send + 'static,
{
    type ReqBody = T::ReqBody;
    type ResBody = T::ResBody;
    type Error = T::Error;
    type Future = Box<
        dyn Future<
                Item = <<T as Service>::Future as Future>::Item,
                Error = <<T as Service>::Future as Future>::Error,
            > + Send,
    >;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let label = self.label.clone();
        let uri = req.uri().query().map_or_else(
            || req.uri().path().to_string(),
            |q| format!("{}?{}", req.uri().path(), q),
        );
        let request = format!("{} {} {:?}", req.method(), uri, req.version());
        let user_agent = req
            .headers()
            .get(USER_AGENT)
            .and_then(|ua| ua.to_str().ok())
            .unwrap_or_else(|| "-")
            .to_string();
        let pid = req
            .extensions()
            .get::<Pid>()
            .map_or_else(|| "-".to_string(), |p| p.to_string());

        let inner = self.inner.call(req);

        Box::new(inner.map(move |response| {
            let body_length = response
                .headers()
                .get(CONTENT_LENGTH)
                .and_then(|l| l.to_str().ok().map(|l| l.to_string()))
                .unwrap_or_else(|| "-".to_string());

            info!(
                "[{}] - - - [{}] \"{}\" {} {} \"-\" \"{}\" pid({})",
                label,
                Utc::now(),
                request,
                response.status(),
                body_length,
                user_agent,
                pid,
            );

            response
        }))
    }
}

impl<T> NewService for LoggingService<T>
where
    T: NewService,
    <T as NewService>::Future: Send + 'static,
    LoggingService<<T as NewService>::Service>: Service,
    // <<T as NewService>::Service as Service>::Future: Send + 'static,
{
    type ReqBody = <LoggingService<<T as NewService>::Service> as Service>::ReqBody;
    type ResBody = <LoggingService<<T as NewService>::Service> as Service>::ResBody;
    type Error = <LoggingService<<T as NewService>::Service> as Service>::Error;
    type Service = LoggingService<<T as NewService>::Service>;
    type Future = Box<dyn Future<Item = Self::Service, Error = Self::InitError> + Send>;
    type InitError = <T as NewService>::InitError;

    fn new_service(&self) -> Self::Future {
        let label = self.label.clone();
        Box::new(
            self.inner
                .new_service()
                .map(|inner| LoggingService { label, inner }),
        )
    }
}
