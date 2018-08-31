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
        Ok(
            MaybeProxyClient {
                client: match proxy_uri {
                    None => Client::new(handle)?,
                    Some(uri) => Client::new_with_proxy(handle, uri)?,
                },
            }
        )
    }

    #[cfg(test)]
    pub fn new_null(proxy_uri: Option<Uri>) -> Result<MaybeProxyClient, Error> {
        Ok(
            MaybeProxyClient {
                client: match proxy_uri {
                    None => Client::new_null()?,
                    Some(_) => Client::new_null_with_proxy()?,
                },
            }
        )
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
    use hyper::Uri;
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
}