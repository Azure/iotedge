// Copyright (c) Microsoft. All rights reserved.

use hyper::{Body, Request, Uri};

use super::super::client::ClientImpl;
use super::hyperwrap::Client;
use super::super::PemCertificate;
use crate::error::Error;

#[derive(Clone)]
pub struct MaybeProxyClient {
    client: Client,
}

impl MaybeProxyClient
{
    pub fn new(proxy_uri: Option<Uri>) -> Result<Self, Error> {
        MaybeProxyClient::create(false, proxy_uri, None)
    }

    pub fn new_with_identity_cert(proxy_uri: Option<Uri>, identity_certificate: PemCertificate) -> Result<Self, Error> {
        MaybeProxyClient::create(false, proxy_uri, Some(identity_certificate))
    }

    fn create(null: bool, proxy_uri: Option<Uri>, identity_certificate: Option<PemCertificate>) -> Result<Self, Error> {
        let mut config = Client::configure();
        if null {
            config.null();
        }
        if let Some(uri) = proxy_uri {
            config.proxy(uri);
        }
        if let Some(id_cert) = identity_certificate {
            config.identity_certificate(id_cert);
        }
        Ok(MaybeProxyClient {
            client: config.build()?,
        })
    }

    #[cfg(test)]
    pub fn new_null() -> Result<Self, Error> {
        MaybeProxyClient::create(true, None, None)
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

impl ClientImpl for MaybeProxyClient {
    type Response = <Client as ClientImpl>::Response;

    fn call(&self, req: Request<Body>) -> Self::Response {
        self.client.call(req)
    }
}

#[cfg(test)]
mod tests {
    use super::super::super::client::ClientImpl;
    use super::MaybeProxyClient;
    use futures::Future;
    use hyper::{Request, StatusCode, Uri};

    #[test]
    fn can_create_client() {
        let client = MaybeProxyClient::new(None).unwrap();
        assert!(!client.has_proxy() && !client.is_null());
    }

    #[test]
    fn can_create_client_with_identity_cert() {
        let pc = PemCertificate::new("cert".as_bytes(), "key".as_bytes(), None, None);
        let client = MaybeProxyClient::new_with_identity_cert(pc).unwrap();
        assert!(!client.has_proxy() && !client.is_null());
    }

    #[test]
    fn can_create_client_with_proxy() {
        let uri = "http://example.com".parse::<Uri>().unwrap();
        let client = MaybeProxyClient::new(Some(uri)).unwrap();
        assert!(client.has_proxy() && !client.is_null());
    }

    #[test]
    fn client_calls_underlying_service() {
        let client = MaybeProxyClient::new_null().unwrap();
        let response = client.call(Request::default()).wait().unwrap();
        assert_eq!(
            response.status(),
            StatusCode::from_u16(234).expect("StatusCode::from_u16 should not fail")
        );
    }
}
