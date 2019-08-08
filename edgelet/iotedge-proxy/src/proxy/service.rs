// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use failure::{Compat, Fail};
use futures::future::FutureResult;
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Request, Response};
use log::debug;

use crate::proxy::{Client, HttpClient, TokenSource};
use crate::{logging, Error};

pub struct ProxyService<T, S>
where
    T: TokenSource,
{
    client: Arc<Client<T, S>>,
}

impl<T, S> ProxyService<T, S>
where
    T: TokenSource,
{
    pub fn new(client: Client<T, S>) -> Self {
        ProxyService {
            client: Arc::new(client),
        }
    }
}

impl<T, S> Clone for ProxyService<T, S>
where
    T: TokenSource,
{
    fn clone(&self) -> Self {
        ProxyService {
            client: self.client.clone(),
        }
    }
}

impl<T, S> Service for ProxyService<T, S>
where
    T: TokenSource + 'static,
    S: HttpClient + 'static,
{
    type ReqBody = Body;
    type ResBody = Body;
    type Error = Compat<Error>;
    type Future = Box<dyn Future<Item = Response<Self::ResBody>, Error = Self::Error> + Send>;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let request = format!("{} {} {:?}", req.method(), req.uri(), req.version());
        debug!("Starting request {}", request);

        let fut = self
            .client
            .request(req)
            .map_err(|err| {
                logging::failure(&err);
                err.compat()
            })
            .map(move |res| {
                debug!("Finished request {}", request);
                res
            });

        Box::new(fut)
    }
}

impl<T, S> NewService for ProxyService<T, S>
where
    T: TokenSource + 'static,
    S: HttpClient + 'static,
{
    type ReqBody = Body;
    type ResBody = Body;
    type Error = Compat<Error>;
    type Service = Self;
    type Future = FutureResult<Self::Service, Self::InitError>;
    type InitError = Compat<Error>;

    fn new_service(&self) -> Self::Future {
        future::ok(self.clone())
    }
}
