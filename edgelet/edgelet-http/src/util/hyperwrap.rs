// Copyright (c) Microsoft. All rights reserved.

use error::Error;
use futures::future;
use hyper::client::{HttpConnector, Service};
use hyper::{Client as HyperClient, Error as HyperError, Request, Response, StatusCode, Uri};
use hyper_proxy::{Intercept, Proxy, ProxyConnector};
use hyper_tls::HttpsConnector;
use tokio_core::reactor::Handle;

const DNS_WORKER_THREADS: usize = 4;

#[derive(Clone, Debug)]
pub struct Config {
    handle: Option<Handle>,
    proxy_uri: Option<Uri>,
    null: bool,
}

impl Config {
    pub fn handle<'a>(&'a mut self, handle: &Handle) -> &'a mut Config {
        self.handle = Some(handle.clone());
        self
    }

    pub fn proxy<'a>(&'a mut self, uri: Uri) -> &'a mut Config {
        self.proxy_uri = Some(uri);
        self
    }

    pub fn null<'a>(&'a mut self) -> &'a mut Config {
        self.null = true;
        self
    }

    pub fn build(&self)  -> Result<Client, Error> {
        match self.null {
            true => Ok(Client::Null),
            false => {
                let config = self.clone();
                let h = &config.handle.expect("tokio_core::reactor::Handle expected!");
                let https = HttpsConnector::new(DNS_WORKER_THREADS, &h)?;
                match config.proxy_uri {
                    None => {
                        Ok(Client::NoProxy(HyperClient::configure().connector(https).build(h)))
                    },
                    Some(uri) => {
                        let proxy = Proxy::new(Intercept::All, uri);
                        let conn = ProxyConnector::from_proxy(https, proxy)?;
                        Ok(Client::Proxy(HyperClient::configure().connector(conn).build(h)))
                    },
                }
            },
        }
    }
}

#[derive(Clone, Debug)]
pub enum Client {
    NoProxy(HyperClient<HttpsConnector<HttpConnector>>),
    Proxy(HyperClient<ProxyConnector<HttpsConnector<HttpConnector>>>),
    Null,
}

impl Client {
    pub fn configure() -> Config {
        Config {
            handle: None,
            proxy_uri: None,
            null: false,
        }
    }

    #[cfg(test)]
    pub fn is_null(&self) -> bool {
        match *self {
            Client::Null => true,
            _ => false,
        }
    }

    #[cfg(test)]
    pub fn has_proxy(&self) -> bool {
        match *self {
            Client::Proxy(_) => true,
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
            Client::Null => Box::new(future::ok(Response::new().with_status(StatusCode::Unregistered(234)))),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::Client;
    use hyper::Uri;
    use tokio_core::reactor::Core;

    // test that the client builder (Config) is wired up correctly to create the
    // right enum variants

    #[test]
    fn can_create_null_client() {
        let client = Client::configure().null().build().unwrap();
        assert!(client.is_null());
    }

    #[test]
    fn can_create_null_client_with_handle() {
        let h = Core::new().unwrap().handle();
        let client = Client::configure().null().handle(&h).build().unwrap();
        assert!(client.is_null());
    }

    #[test]
    fn can_create_null_client_with_proxy() {
        let uri = "irrelevant".parse::<Uri>().unwrap();
        let client = Client::configure().null().proxy(uri).build().unwrap();
        assert!(client.is_null());
    }

    #[test]
    fn can_create_null_client_with_everything() {
        let h = Core::new().unwrap().handle();
        let uri = "irrelevant".parse::<Uri>().unwrap();
        let client = Client::configure().null().handle(&h).proxy(uri).build().unwrap();
        assert!(client.is_null());
    }

    #[test]
    #[should_panic(expected = "tokio_core::reactor::Handle expected!")]
    fn cannot_create_client_without_handle() {
        Client::configure().build().unwrap();
    }

    #[test]
    fn can_create_client() {
        let h = Core::new().unwrap().handle();
        let client = Client::configure().handle(&h).build().unwrap();
        assert!(!client.has_proxy() && !client.is_null());
    }

    #[test]
    fn can_create_client_with_proxy() {
        let h = Core::new().unwrap().handle();
        let uri = "irrelevant".parse::<Uri>().unwrap();
        let client = Client::configure().handle(&h).proxy(uri).build().unwrap();
        assert!(client.has_proxy());
    }

    // TODO:
    // test that Client::Proxy and Client::NoProxy can actually be used to make
    // HTTPS requests with or without a proxy (respectively)
}
