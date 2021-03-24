// Copyright (c) Microsoft. All rights reserved.

use failure::{Fail, ResultExt};
use futures::future::Future;
use futures::prelude::*;
use http::Uri;
use hyper::client::Client as HyperClient;
use hyper::{Body, Client};
use typed_headers::{self, http};

use edgelet_core::UrlExt;
use edgelet_http::UrlConnector;

use crate::error::{Error, ErrorKind};
use url::Url;

#[derive(Clone)]
pub struct IdentityClient {
    api_version: aziot_identity_common_http::ApiVersion,
    client: HyperClient<UrlConnector, Body>,
    host: Url,
}

impl IdentityClient {
    pub fn new(api_version: aziot_identity_common_http::ApiVersion, url: &url::Url) -> Self {
        let client = Client::builder().build(UrlConnector::new(&url).expect("Hyper client"));
        IdentityClient {
            api_version,
            client,
            host: url.clone(),
        }
    }

    pub fn get_device(
        &self,
    ) -> Box<dyn Future<Item = aziot_identity_common::Identity, Error = Error> + Send> {
        let client = self.client.clone();
        let uri = format!("/identities/device?api-version={}", self.api_version);
        let body = serde_json::json! {{ "type": "aziot" }};

        let identity = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| request(&client, hyper::Method::POST, &uri, Some(&body)));

        Box::new(identity)
    }

    pub fn reprovision_device(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
        let client = self.client.clone();
        let uri = format!(
            "/identities/device/reprovision?api-version={}",
            self.api_version
        );
        let body = serde_json::json! {{ "type": "aziot" }};

        let res = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| {
                request_no_content::<_, _>(&client, hyper::Method::POST, &uri, Some(&body))
            });

        Box::new(res)
    }

    pub fn create_module(
        &self,
        module_name: &str,
    ) -> Box<dyn Future<Item = aziot_identity_common::Identity, Error = Error> + Send> {
        let client = self.client.clone();
        let uri = format!(
            "/identities/modules?api-version={}&type=aziot",
            self.api_version
        );
        let body = serde_json::json! {{ "type": "aziot", "moduleId" : module_name }};

        let identity = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| request(&client, hyper::Method::POST, &uri, Some(&body)));

        Box::new(identity)
    }

    pub fn update_module(
        &self,
        module_name: &str,
    ) -> Box<dyn Future<Item = aziot_identity_common::Identity, Error = Error> + Send> {
        let client = self.client.clone();
        let uri = format!(
            "/identities/modules/{}?api-version={}&type=aziot",
            module_name, self.api_version
        );
        let body = serde_json::json! {{ "type": "aziot", "moduleId" : module_name }};

        let identity = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| request(&client, hyper::Method::PUT, &uri, Some(&body)));

        Box::new(identity)
    }

    pub fn delete_module(
        &self,
        module_name: &str,
    ) -> Box<dyn Future<Item = (), Error = Error> + Send> {
        let client = self.client.clone();
        let uri = format!(
            "/identities/modules/{}?api-version={}&type=aziot",
            module_name, self.api_version
        );

        let res = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| {
                request_no_content::<_, ()>(&client, hyper::Method::DELETE, &uri, None)
            });

        Box::new(res)
    }

    pub fn get_module(
        &self,
        module_name: &str,
    ) -> Box<dyn Future<Item = aziot_identity_common::Identity, Error = Error> + Send> {
        let client = self.client.clone();
        let uri = format!(
            "/identities/modules/{}?api-version={}&type=aziot",
            module_name, self.api_version
        );
        let body = serde_json::json! {{ "type": "aziot", "moduleId" : module_name }};

        let identity = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| request(&client, hyper::Method::GET, &uri, Some(&body)));

        Box::new(identity)
    }

    pub fn get_modules(
        &self,
    ) -> Box<dyn Future<Item = Vec<aziot_identity_common::Identity>, Error = Error> + Send> {
        let client = self.client.clone();
        let uri = format!(
            "/identities/modules?api-version={}&type=aziot",
            self.api_version
        );

        let identities = build_request_uri(&self.host, &uri)
            .into_future()
            .and_then(move |uri| {
                request::<_, (), aziot_identity_common_http::get_module_identities::Response>(
                    &client,
                    hyper::Method::GET,
                    &uri,
                    None,
                )
                .map(|identities| identities.identities)
            });

        Box::new(identities)
    }
}

fn build_request_uri(host: &Url, uri: &str) -> Result<Uri, Error> {
    let base_path = host.to_base_path().context(ErrorKind::ConnectorUri)?;
    UrlConnector::build_hyper_uri(
        &host.scheme().to_string(),
        &base_path
            .to_str()
            .ok_or(ErrorKind::ConnectorUri)?
            .to_string(),
        &uri,
    )
    .map_err(|_| Error::from(ErrorKind::ConnectorUri))
}

fn request<TConnect, TRequest, TResponse>(
    client: &hyper::Client<TConnect, hyper::Body>,
    method: http::Method,
    uri: &http::Uri,
    body: Option<&TRequest>,
) -> Box<dyn Future<Item = TResponse, Error = Error> + Send>
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
            .map_err(|e| Error::from(e.context(ErrorKind::Request)))
            .and_then(|resp| {
                let (
                    http::response::Parts {
                        status, headers, ..
                    },
                    body,
                ) = resp.into_parts();
                body.concat2()
                    .and_then(move |body| Ok((status, headers, body)))
                    .map_err(|e| Error::from(e.context(ErrorKind::Hyper)))
            })
            .and_then(|(status, headers, body)| {
                if status.is_success() {
                    let mut is_json = false;
                    for (header_name, header_value) in headers {
                        if header_name == Some(hyper::header::CONTENT_TYPE) {
                            let value = header_value
                                .to_str()
                                .map_err(|_| Error::from(ErrorKind::MalformedResponse))?;
                            if value == "application/json" {
                                is_json = true;
                            }
                        }
                    }

                    if !is_json {
                        return Err(Error::from(ErrorKind::MalformedResponse));
                    }

                    Ok(body)
                } else {
                    Err(Error::http_with_error_response(status, &*body))
                }
            })
            .and_then(|body| {
                let parsed: Result<TResponse, _> = serde_json::from_slice(&body);
                parsed.map_err(|e| Error::from(ErrorKind::Serde(e)))
            }),
    )
}

fn request_no_content<TConnect, TRequest>(
    client: &hyper::Client<TConnect, hyper::Body>,
    method: http::Method,
    uri: &http::Uri,
    body: Option<&TRequest>,
) -> Box<dyn Future<Item = (), Error = Error> + Send>
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
            .map_err(|e| Error::from(e.context(ErrorKind::Request)))
            .and_then(|resp| {
                let (http::response::Parts { status, .. }, body) = resp.into_parts();
                body.concat2()
                    .and_then(move |body| Ok((status, body)))
                    .map_err(|e| Error::from(e.context(ErrorKind::Hyper)))
            })
            .and_then(|(status, body)| {
                if status.is_success() {
                    Ok(())
                } else {
                    Err(Error::http_with_error_response(status, &*body))
                }
            }),
    )
}
