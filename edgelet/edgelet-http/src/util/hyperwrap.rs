// Copyright (c) Microsoft. All rights reserved.

use error::Error;
use futures::future;
use hyper::client::HttpConnector;
use hyper::{Body, Client as HyperClient, Error as HyperError, Request, Response, StatusCode, Uri};
use hyper_proxy::{Intercept, Proxy, ProxyConnector};
use hyper_tls::HttpsConnector;

use super::super::client::ClientImpl;

const DNS_WORKER_THREADS: usize = 4;

#[derive(Clone, Debug)]
pub struct Config {
    proxy_uri: Option<Uri>,
    null: bool,
}

impl Config {
    pub fn proxy(&mut self, uri: Uri) -> &mut Config {
        self.proxy_uri = Some(uri);
        self
    }

    pub fn null(&mut self) -> &mut Config {
        self.null = true;
        self
    }

    pub fn build(&self) -> Result<Client, Error> {
        if self.null {
            Ok(Client::Null)
        } else {
            let config = self.clone();
            let https = HttpsConnector::new(DNS_WORKER_THREADS)?;
            match config.proxy_uri {
                None => Ok(Client::NoProxy(HyperClient::builder().build(https))),
                Some(uri) => {
                    let proxy = Proxy::new(Intercept::All, uri);
                    let conn = ProxyConnector::from_proxy(https, proxy)?;
                    Ok(Client::Proxy(HyperClient::builder().build(conn)))
                }
            }
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

impl ClientImpl for Client {
    type Response = Box<future::Future<Item = Response<Body>, Error = HyperError> + Send>;

    fn call(&self, req: Request<Body>) -> Self::Response {
        match *self {
            Client::NoProxy(ref client) => Box::new(client.request(req)) as Self::Response,
            Client::Proxy(ref client) => Box::new(client.request(req)) as Self::Response,
            Client::Null => Box::new(future::ok(
                Response::builder()
                    .status(
                        StatusCode::from_u16(234).expect("StatusCode::from_u16 should not fail"),
                    ).body(Body::empty())
                    .expect("creating empty resposne should not fail"),
            )),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::Client;
    use hyper::Uri;

    // test that the client builder (Config) is wired up correctly to create the
    // right enum variants

    #[test]
    fn can_create_null_client() {
        let client = Client::configure().null().build().unwrap();
        assert!(client.is_null());
    }

    #[test]
    fn can_create_null_client_with_proxy() {
        let uri = "irrelevant".parse::<Uri>().unwrap();
        let client = Client::configure().null().proxy(uri).build().unwrap();
        assert!(client.is_null());
    }

    #[test]
    fn can_create_client() {
        let client = Client::configure().build().unwrap();
        assert!(!client.has_proxy() && !client.is_null());
    }

    #[test]
    fn can_create_client_with_proxy() {
        let uri = "irrelevant".parse::<Uri>().unwrap();
        let client = Client::configure().proxy(uri).build().unwrap();
        assert!(client.has_proxy());
    }

    // TODO:
    // test that Client::Proxy and Client::NoProxy can actually be used to make
    // HTTPS requests with or without a proxy (respectively)
}
