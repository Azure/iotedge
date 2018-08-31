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
        MaybeProxyClient::create(Some(handle), proxy_uri)
    }

    fn create(handle: Option<&Handle>, proxy_uri: Option<Uri>) -> Result<MaybeProxyClient, Error> {
        let mut config = Client::configure();
        if let Some(h) = handle {
            config.handle(h);
        } else {
            config.null();
        }
        if let Some(uri) = proxy_uri {
            config.proxy(uri);
        }
        Ok(
            MaybeProxyClient {
                client: config.build()?
            }
        )
    }

    #[cfg(test)]
    pub fn new_null(proxy_uri: Option<Uri>) -> Result<MaybeProxyClient, Error> {
        MaybeProxyClient::create(None, proxy_uri)
    }

    #[cfg(test)]
    pub fn is_null(&self) -> bool {
        self.client.is_null()
    }

    #[cfg(test)]
    pub fn has_proxy(&self) -> bool {
        self.client.has_proxy()
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

#[cfg(test)]
mod tests {
    use futures::Future;
    use http::Request;
    use hyper::{StatusCode, Uri};
    use hyper::client::Service;
    use super::MaybeProxyClient;
    use tokio_core::reactor::Core;

    #[test]
    fn can_create_client() {
        let handle = Core::new().unwrap().handle();
        let client = MaybeProxyClient::new(&handle, None).unwrap();
        assert!(!client.has_proxy() && !client.is_null());
    }

    #[test]
    fn can_create_client_with_proxy() {
        let handle = Core::new().unwrap().handle();
        let uri = "irrelevant".parse::<Uri>().unwrap();
        let client = MaybeProxyClient::new(&handle, Some(uri)).unwrap();
        assert!(client.has_proxy());
    }

    #[test]
    fn can_create_null_client() {
        let client = MaybeProxyClient::new_null(None).unwrap();
        assert!(client.is_null() && !client.has_proxy());
    }

    #[test]
    fn can_create_null_client_with_proxy() {
        let uri = "irrelevant".parse::<Uri>().unwrap();
        let client = MaybeProxyClient::new_null(Some(uri)).unwrap();
        assert!(client.is_null() && client.has_proxy());
    }

    #[test]
    fn client_calls_underlying_service() {
        let client = MaybeProxyClient::new_null(None).unwrap();
        let response = client.call(Request::default().into()).wait().unwrap();
        assert_eq!(response.status(), StatusCode::Ok);
    }
}