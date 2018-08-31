// Copyright (c) Microsoft. All rights reserved.

use error::Error;
use futures::future;
use hyper::client::{HttpConnector, Service};
use hyper::{Client as HyperClient, Error as HyperError, Request, Response, Uri};
use hyper_proxy::{Intercept, Proxy, ProxyConnector};
use hyper_tls::HttpsConnector;
use tokio_core::reactor::Handle;

const DNS_WORKER_THREADS: usize = 4;

#[derive(Clone, Debug)]
pub enum Client {
    NoProxy(HyperClient<HttpsConnector<HttpConnector>>),
    Proxy(HyperClient<ProxyConnector<HttpsConnector<HttpConnector>>>),
    #[cfg(test)]
    NullNoProxy,
    #[cfg(test)]
    NullProxy,
}

impl Client {
    pub fn new(handle: &Handle) -> Result<Client, Error> {
        let https = HttpsConnector::new(DNS_WORKER_THREADS, &handle.clone())?;
        Ok(Client::NoProxy(
            HyperClient::configure().connector(https).build(&handle),
        ))
    }

    pub fn new_with_proxy(handle: &Handle, uri: Uri) -> Result<Client, Error> {
        let https = HttpsConnector::new(DNS_WORKER_THREADS, &handle.clone())?;
        let proxy = Proxy::new(Intercept::All, uri);
        let connector = ProxyConnector::from_proxy(https, proxy)?;
        Ok(Client::Proxy(
            HyperClient::configure().connector(connector).build(&handle),
        ))
    }

    #[cfg(test)]
    pub fn new_null() -> Result<Client, Error> {
        Ok(Client::NullNoProxy)
    }

    #[cfg(test)]
    pub fn new_null_with_proxy() -> Result<Client, Error> {
        Ok(Client::NullProxy)
    }

    #[cfg(test)]
    pub fn is_null(&self) -> bool {
        match *self {
            Client::NullNoProxy => true,
            Client::NullProxy => true,
            _ => false,
        }
    }

    #[cfg(test)]
    pub fn has_proxy(&self) -> bool {
        match *self {
            Client::Proxy(_) => true,
            Client::NullProxy => true,
            _ => false,
        }
    }
}

impl Service for Client {
    type Request = Request;
    type Response = Response;
    type Error = HyperError;
    type Future = Box<future::Future<Item = Self::Response, Error = Self::Error>>;

    fn call(&self, req: Self::Request) -> Self::Future {
        match *self {
            Client::NoProxy(ref client) => Box::new(client.call(req)) as Self::Future,
            Client::Proxy(ref client) => Box::new(client.call(req)) as Self::Future,
            #[cfg(test)]
            Client::NullNoProxy => Box::new(future::ok(Response::new())),
            #[cfg(test)]
            Client::NullProxy => Box::new(future::ok(Response::new())),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::Client;
    use hyper::Uri;
    use tokio_core::reactor::Core;

    // test that the factory functions are wired up correctly to their
    // corresponding enum variants

    #[test]
    fn can_create_client() {
        let handle = Core::new().unwrap().handle();
        let client = Client::new(&handle).unwrap();
        assert!(!client.has_proxy() && !client.is_null());
    }

    #[test]
    fn can_create_client_with_proxy() {
        let handle = Core::new().unwrap().handle();
        let uri = "irrelevant".parse::<Uri>().unwrap();
        let client = Client::new_with_proxy(&handle, uri).unwrap();
        assert!(client.has_proxy());
    }

    #[test]
    fn can_create_null_client() {
        let client = Client::new_null().unwrap();
        assert!(client.is_null() && !client.has_proxy());
    }

    #[test]
    fn can_create_null_client_with_proxy() {
        let client = Client::new_null_with_proxy().unwrap();
        assert!(client.is_null() && client.has_proxy());
    }

    // TODO:
    // test that Client::Proxy and Client::NoProxy can actually be used to make
    // HTTPS requests with or without a proxy
}
