// Copyright (c) Microsoft. All rights reserved.

use anyhow::Context;
use futures::future::Future;
use futures::prelude::*;
use http::Uri;
use hyper::client::Client as HyperClient;
use hyper::{Body, Client};
use typed_headers::{self, http};

use edgelet_core::UrlExt;
use edgelet_http::UrlConnector;

use crate::error::Error;
use url::Url;

/// Ref <https://url.spec.whatwg.org/#path-percent-encode-set>
pub const PATH_SEGMENT_ENCODE_SET: &percent_encoding::AsciiSet = &percent_encoding::CONTROLS
    .add(b' ')
    .add(b'"')
    .add(b'<')
    .add(b'>')
    .add(b'`') // fragment percent-encode set
    .add(b'#')
    .add(b'?')
    .add(b'{')
    .add(b'}'); // path percent-encode set

#[derive(Clone)]
pub struct CertificateClient {
    api_version: aziot_cert_common_http::ApiVersion,
    client: HyperClient<UrlConnector, Body>,
    host: Url,
}

impl CertificateClient {
    pub fn new(api_version: aziot_cert_common_http::ApiVersion, url: &url::Url) -> Self {
        let client = Client::builder().build(UrlConnector::new(&url).expect("Hyper client"));
        CertificateClient {
            api_version,
            client,
            host: url.clone(),
        }
    }

    pub fn create_cert(
        &self,
        id: &str,
        csr: &[u8],
        issuer: Option<(&str, &aziot_key_common::KeyHandle)>,
    ) -> Box<dyn Future<Item = Vec<u8>, Error = anyhow::Error> + Send> {
        let client = self.client.clone();
        let uri = format!("/certificates?api-version={}", self.api_version);
        let body = aziot_cert_common_http::create_cert::Request {
            cert_id: id.to_owned(),
            csr: aziot_cert_common_http::Pem(csr.to_owned()),
            issuer: issuer.map(|(cert_id, private_key_handle)| {
                aziot_cert_common_http::create_cert::Issuer {
                    cert_id: cert_id.to_owned(),
                    private_key_handle: private_key_handle.clone(),
                }
            }),
        };

        let res = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| {
                request::<
                    _,
                    aziot_cert_common_http::create_cert::Request,
                    aziot_cert_common_http::create_cert::Response,
                >(&client, hyper::Method::POST, &uri, Some(&body))
                .map(|res| res.pem.0)
            });

        Box::new(res)
    }

    pub fn import_cert(
        &self,
        id: &str,
        pem: &[u8],
    ) -> Box<dyn Future<Item = Vec<u8>, Error = anyhow::Error> + Send> {
        let client = self.client.clone();
        let uri = format!(
            "/certificates/{}?api-version={}",
            percent_encoding::percent_encode(id.as_bytes(), PATH_SEGMENT_ENCODE_SET),
            self.api_version
        );
        let body = aziot_cert_common_http::import_cert::Request {
            pem: aziot_cert_common_http::Pem(pem.to_owned()),
        };

        let res = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| {
                request::<
                    _,
                    aziot_cert_common_http::import_cert::Request,
                    aziot_cert_common_http::import_cert::Response,
                >(&client, hyper::Method::POST, &uri, Some(&body))
                .map(|res| res.pem.0)
            });

        Box::new(res)
    }

    pub fn get_cert(
        &self,
        id: &str,
    ) -> Box<dyn Future<Item = Vec<u8>, Error = anyhow::Error> + Send> {
        let client = self.client.clone();
        let uri = format!(
            "/certificates/{}?api-version={}",
            percent_encoding::percent_encode(id.as_bytes(), PATH_SEGMENT_ENCODE_SET),
            self.api_version
        );

        let res = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| {
                request::<_, (), aziot_cert_common_http::get_cert::Response>(
                    &client,
                    hyper::Method::GET,
                    &uri,
                    None,
                )
                .map(|res| res.pem.0)
            });

        Box::new(res)
    }

    pub fn delete_cert(
        &self,
        id: &str,
    ) -> Box<dyn Future<Item = (), Error = anyhow::Error> + Send> {
        let client = self.client.clone();
        let uri = format!(
            "{}certificates/{}?api-version={}",
            self.host.as_str(),
            percent_encoding::percent_encode(id.as_bytes(), PATH_SEGMENT_ENCODE_SET),
            self.api_version
        );

        let res = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| {
                request_no_content::<_, ()>(&client, hyper::Method::DELETE, &uri, None)
            });

        Box::new(res)
    }
}

fn build_request_uri(host: &Url, uri: &str) -> anyhow::Result<Uri> {
    let base_path = host.to_base_path().context(Error::ConnectorUri)?;
    UrlConnector::build_hyper_uri(
        &host.scheme().to_string(),
        &base_path.to_str().context(Error::ConnectorUri)?.to_string(),
        &uri,
    )
    .context(Error::ConnectorUri)
}

fn request<TConnect, TRequest, TResponse>(
    client: &hyper::Client<TConnect, hyper::Body>,
    method: http::Method,
    uri: &http::Uri,
    body: Option<&TRequest>,
) -> Box<dyn Future<Item = TResponse, Error = anyhow::Error> + Send>
where
    TConnect: hyper::client::connect::Connect + Clone + Send + Sync + 'static,
    TRequest: serde::Serialize,
    TResponse: serde::de::DeserializeOwned + Send + 'static,
{
    let mut builder = hyper::Request::builder();
    builder.method(method).uri(uri);

    // `builder` is consumed by both branches, so this cannot be replaced with `Option::map_or_else`
    //
    // Ref: https://github.com/rust-lang/rust-clippy/issues/5822
    #[allow(clippy::option_if_let_else)]
    let builder = if let Some(body) = body {
        let body = serde_json::to_vec(body)
            .expect("serializing request body to JSON cannot fail")
            .into();
        builder
            .header(hyper::header::CONTENT_TYPE, "application/json")
            .body(body)
    } else {
        builder.body(hyper::Body::default())
    };

    let req = builder.expect("cannot fail to create hyper request");

    Box::new(
        client
            .request(req)
            .map_err(|e| anyhow::anyhow!(e).context(Error::Request))
            .and_then(|resp| {
                let (
                    http::response::Parts {
                        status, headers, ..
                    },
                    body,
                ) = resp.into_parts();
                body.concat2()
                    .and_then(move |body| Ok((status, headers, body)))
                    .map_err(|e| anyhow::anyhow!(e).context(Error::Hyper))
            })
            .and_then(|(status, headers, body)| {
                if status.is_success() {
                    let mut is_json = false;
                    for (header_name, header_value) in headers {
                        if header_name == Some(hyper::header::CONTENT_TYPE) {
                            let value = header_value.to_str().context(Error::MalformedResponse)?;
                            if value == "application/json" {
                                is_json = true;
                            }
                        }
                    }

                    anyhow::ensure!(is_json, Error::MalformedResponse);

                    Ok(body)
                } else {
                    anyhow::bail!(Error::from((status, &*body)))
                }
            })
            .and_then(|body| Ok(serde_json::from_slice(&body)?)),
    )
}

fn request_no_content<TConnect, TRequest>(
    client: &hyper::Client<TConnect, hyper::Body>,
    method: http::Method,
    uri: &http::Uri,
    body: Option<&TRequest>,
) -> Box<dyn Future<Item = (), Error = anyhow::Error> + Send>
where
    TConnect: hyper::client::connect::Connect + Clone + Send + Sync + 'static,
    TRequest: serde::Serialize,
{
    let mut builder = hyper::Request::builder();
    builder.method(method).uri(uri);

    // `builder` is consumed by both branches, so this cannot be replaced with `Option::map_or_else`
    //
    // Ref: https://github.com/rust-lang/rust-clippy/issues/5822
    #[allow(clippy::option_if_let_else)]
    let builder = if let Some(body) = body {
        let body = serde_json::to_vec(body)
            .expect("serializing request body to JSON cannot fail")
            .into();
        builder
            .header(hyper::header::CONTENT_TYPE, "application/json")
            .body(body)
    } else {
        builder.body(hyper::Body::default())
    };

    let req = builder.expect("cannot fail to create hyper request");

    Box::new(
        client
            .request(req)
            .map_err(|e| anyhow::anyhow!(e).context(Error::Request))
            .and_then(|resp| {
                let (http::response::Parts { status, .. }, body) = resp.into_parts();
                body.concat2()
                    .and_then(move |body| Ok((status, body)))
                    .map_err(|e| anyhow::anyhow!(e).context(Error::Hyper))
            })
            .and_then(|(status, body)| {
                if status.is_success() {
                    Ok(())
                } else {
                    anyhow::bail!(Error::from((status, &*body)))
                }
            }),
    )
}
