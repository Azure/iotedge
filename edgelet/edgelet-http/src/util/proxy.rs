// Copyright (c) Microsoft. All rights reserved.

use super::hyperwrap::Client;
use error::Error;
use hyper::client::Service;
use hyper::Uri;
use tokio_core::reactor::Handle;

#[derive(Clone)]
pub struct MaybeProxyClient {
    client: Client,
}

impl MaybeProxyClient {
    pub fn new(handle: &Handle, proxy_uri: Option<Uri>) -> Result<MaybeProxyClient, Error> {
        Ok(MaybeProxyClient {
            client: match proxy_uri {
                None => Client::new(handle)?,
                Some(uri) => Client::new_with_proxy(handle, uri)?,
            },
        })
    }
}

impl Service for MaybeProxyClient {
    type Request = <Client as Service>::Request;
    type Response = <Client as Service>::Response;
    type Error = <Client as Service>::Error;
    type Future = <Client as Service>::Future;

    fn call(&self, req: Self::Request) -> Self::Future {
        self.client.call(req)
    }
}
