// Copyright (c) Microsoft. All rights reserved.

use futures::Future;
use hyper::client::connect::Connect;
use hyper::Request;
use hyper::{client::ResponseFuture, service::Service, Body, Client as HyperClient};

#[derive(Clone)]
pub struct HyperClientService<C>(HyperClient<C>);

impl<C> HyperClientService<C> {
    pub fn new(client: HyperClient<C>) -> HyperClientService<C> {
        HyperClientService(client)
    }
}

impl<C: Connect + 'static> Service for HyperClientService<C> {
    type ReqBody = Body;
    type ResBody = Body;
    type Error = <ResponseFuture as Future>::Error;
    type Future = ResponseFuture;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        self.0.request(req)
    }
}
