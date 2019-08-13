// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::future;
use hyper::client::HttpConnector;
use hyper::{Body, Client as HyperClient, Error as HyperError, Request, Response, StatusCode, Uri};
use hyper_proxy::{Intercept, Proxy, ProxyConnector};
use hyper_tls::HttpsConnector;
use native_tls::{Certificate as TlsCertificate, TlsConnector};
use openssl::x509::X509;
use typed_headers::Credentials;
use url::percent_encoding::percent_decode;
use url::Url;

use crate::client::ClientImpl;
use crate::error::{Error, ErrorKind, InvalidUrlReason};
use crate::PemCertificate;

const DNS_WORKER_THREADS: usize = 4;

#[derive(Clone, Debug)]
pub struct Config {
    proxy_uri: Option<Uri>,
    null: bool,
    identity_certificate: Option<PemCertificate>,
    trust_bundle: Option<PemCertificate>,
}

impl Config {
    pub fn identity_certificate(&mut self, identity_cert: PemCertificate) -> &mut Config {
        self.identity_certificate = Some(identity_cert);
        self
    }

    pub fn trust_bundle(&mut self, trust_bundle: PemCertificate) -> &mut Config {
        self.trust_bundle = Some(trust_bundle);
        self
    }

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
            let mut builder = TlsConnector::builder();
            if let Some(bundle) = &self.trust_bundle {
                let certs = X509::stack_from_pem(&bundle.cert)
                    .context(ErrorKind::TrustBundle)
                    .context(ErrorKind::Initialization)?;
                for cert in certs {
                    let der = cert
                        .to_der()
                        .context(ErrorKind::TrustBundle)
                        .context(ErrorKind::Initialization)?;
                    let c = TlsCertificate::from_der(&der)
                        .context(ErrorKind::TrustBundle)
                        .context(ErrorKind::Initialization)?;
                    builder.add_root_certificate(c);
                }
            }
            if let Some(id) = &self.identity_certificate {
                let identity = id.get_identity().context(ErrorKind::Initialization)?;
                builder.identity(identity);
            }

            let connector = builder.build().context(ErrorKind::Initialization)?;
            let mut http = HttpConnector::new(DNS_WORKER_THREADS);
            http.enforce_http(false);
            let https_connector = HttpsConnector::from((http, connector));

            match &self.proxy_uri {
                None => Ok(Client::NoProxy(
                    HyperClient::builder().build(https_connector),
                )),
                Some(uri) => {
                    let proxy = uri_to_proxy(uri.clone())?;
                    let conn = ProxyConnector::from_proxy(https_connector, proxy)
                        .context(ErrorKind::Proxy(uri.clone()))
                        .context(ErrorKind::Initialization)?;
                    Ok(Client::Proxy(HyperClient::builder().build(conn)))
                }
            }
        }
    }
}

fn uri_to_proxy(uri: Uri) -> Result<Proxy, Error> {
    let url = Url::parse(&uri.to_string()).with_context(|_| ErrorKind::Proxy(uri.clone()))?;
    let mut proxy = Proxy::new(Intercept::All, uri.clone());

    if !url.username().is_empty() {
        let username = percent_decode(url.username().as_bytes())
            .decode_utf8()
            .with_context(|_| {
                ErrorKind::InvalidUrlWithReason(
                    url.to_string(),
                    InvalidUrlReason::InvalidCredentials,
                )
            })
            .with_context(|_| ErrorKind::Proxy(uri.clone()))
            .context(ErrorKind::Initialization)?;
        let credentials = match url.password() {
            Some(password) => {
                let password = percent_decode(password.as_bytes())
                    .decode_utf8()
                    .with_context(|_| {
                        ErrorKind::InvalidUrlWithReason(
                            url.to_string(),
                            InvalidUrlReason::InvalidCredentials,
                        )
                    })
                    .with_context(|_| ErrorKind::Proxy(uri.clone()))
                    .context(ErrorKind::Initialization)?;

                Credentials::basic(&username, &password)
                    .with_context(|_| {
                        ErrorKind::InvalidUrlWithReason(
                            url.to_string(),
                            InvalidUrlReason::InvalidCredentials,
                        )
                    })
                    .with_context(|_| ErrorKind::Proxy(uri))
                    .context(ErrorKind::Initialization)?
            }
            None => Credentials::basic(&username, "")
                .with_context(|_| {
                    ErrorKind::InvalidUrlWithReason(
                        url.to_string(),
                        InvalidUrlReason::InvalidCredentials,
                    )
                })
                .with_context(|_| ErrorKind::Proxy(uri))
                .context(ErrorKind::Initialization)?,
        };
        proxy.set_authorization(credentials);
    }

    Ok(proxy)
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
            identity_certificate: None,
            trust_bundle: None,
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
    type Response = Box<dyn future::Future<Item = Response<Body>, Error = HyperError> + Send>;

    fn call(&self, req: Request<Body>) -> Self::Response {
        match *self {
            Client::NoProxy(ref client) => Box::new(client.request(req)) as Self::Response,
            Client::Proxy(ref client) => Box::new(client.request(req)) as Self::Response,
            Client::Null => Box::new(future::ok(
                Response::builder()
                    .status(
                        StatusCode::from_u16(234).expect("StatusCode::from_u16 should not fail"),
                    )
                    .body(Body::empty())
                    .expect("creating empty resposne should not fail"),
            )),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
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
        let uri = "http://example.com".parse::<Uri>().unwrap();
        let client = Client::configure().proxy(uri).build().unwrap();
        assert!(client.has_proxy());
    }

    #[test]
    fn proxy_no_username() {
        let uri = "http://example.com".parse().unwrap();
        let proxy = uri_to_proxy(uri).unwrap();
        assert_eq!(None, proxy.headers().get("Authorization"));
    }

    #[test]
    fn proxy_username() {
        let uri = "http://user100@example.com".parse().unwrap();
        let proxy = uri_to_proxy(uri).unwrap();

        let expected = "Basic dXNlcjEwMDo=";
        assert_eq!(&expected, proxy.headers().get("Authorization").unwrap());
    }

    #[test]
    fn proxy_username_password() {
        let uri = "http://user100:password123@example.com".parse().unwrap();
        let proxy = uri_to_proxy(uri).unwrap();

        let expected = "Basic dXNlcjEwMDpwYXNzd29yZDEyMw==";
        assert_eq!(&expected, proxy.headers().get("Authorization").unwrap());
    }

    // TODO:
    // test that Client::Proxy and Client::NoProxy can actually be used to make
    // HTTPS requests with or without a proxy (respectively)
}
