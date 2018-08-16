// Copyright (c) Microsoft. All rights reserved.

use error::Error;
use hyper::client::{FutureResponse, HttpConnector, Service};
use hyper::{Client as HyperClient, Error as HyperError, Request, Response, Uri};
use hyper_proxy::{Proxy, ProxyConnector, Intercept};
use hyper_tls::HttpsConnector;
use tokio_core::reactor::Handle;

const DNS_WORKER_THREADS: usize = 4;

#[derive(Clone)]
pub enum MaybeProxyClient {
    NoProxy(HyperClient<HttpsConnector<HttpConnector>>),
    Proxy(HyperClient<ProxyConnector<HttpsConnector<HttpConnector>>>),
}

impl MaybeProxyClient {
    pub fn new(handle: &Handle, proxy_uri: Option<Uri>) -> Result<MaybeProxyClient, Error> {
        let https = HttpsConnector::new(DNS_WORKER_THREADS, &handle.clone())?;
        match proxy_uri {
            None => {
                Ok(MaybeProxyClient::NoProxy(HyperClient::configure().connector(https).build(&handle)))
            },
            Some(uri) => {
                let proxy = Proxy::new(Intercept::All, uri);
                let connector = ProxyConnector::from_proxy(https, proxy)?;
                Ok(MaybeProxyClient::Proxy(HyperClient::configure().connector(connector).build(&handle)))
            }
        }
    }
}

impl Service for MaybeProxyClient {
    type Request = Request;
    type Response = Response;
    type Error = HyperError;
    type Future = FutureResponse;

    fn call(&self, req: Self::Request) -> Self::Future {
        match *self {
            MaybeProxyClient::NoProxy(ref client) => client.call(req) as Self::Future,
            MaybeProxyClient::Proxy(ref client) => client.call(req) as Self::Future,
        }
    }
}

