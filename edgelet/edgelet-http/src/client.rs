// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::sync::Arc;

use chrono::{DateTime, Duration, Utc};
use failure::{Fail, ResultExt};
use futures::{Future, IntoFuture, Stream};
use hyper::{self, Body, Method, Request, Response};
use log::debug;
use serde::de::DeserializeOwned;
use serde::Serialize;
use serde_json;
use typed_headers::{http, mime, ContentLength, ContentType, HeaderMapExt};
use url::form_urlencoded::Serializer as UrlSerializer;
use url::Url;

use edgelet_utils::ensure_not_empty_with_context;

use crate::error::{Error, ErrorKind};

pub trait TokenSource {
    type Error;
    fn get(&self, expiry: &DateTime<Utc>) -> Result<String, Self::Error>;
}

pub trait ClientImpl: Send + Sync {
    type Response: Future<Item = Response<Body>, Error = hyper::Error> + Send;

    fn call(&self, req: Request<Body>) -> Self::Response;
}

impl<C> ClientImpl for hyper::Client<C, Body>
where
    C: hyper::client::connect::Connect + Sync + 'static,
    <C as hyper::client::connect::Connect>::Transport: 'static,
    <C as hyper::client::connect::Connect>::Future: 'static,
{
    type Response = hyper::client::ResponseFuture;

    fn call(&self, req: Request<Body>) -> Self::Response {
        self.request(req)
    }
}

impl<F, R> ClientImpl for F
where
    F: Fn(Request<Body>) -> R + Send + Sync,
    R: IntoFuture<Item = Response<Body>, Error = hyper::Error>,
    <R as IntoFuture>::Future: Send,
{
    type Response = <R as IntoFuture>::Future;

    fn call(&self, req: Request<Body>) -> Self::Response {
        (self)(req).into_future()
    }
}

pub struct Client<C, T> {
    inner: Arc<C>,
    token_source: Option<T>,
    api_version: String,
    host_name: Url,
    user_agent: Option<String>,
}

impl<C, T> Client<C, T>
where
    C: ClientImpl,
    T: TokenSource + Clone,
    T::Error: Fail,
{
    pub fn new(
        inner: C,
        token_source: Option<T>,
        api_version: String,
        host_name: Url,
    ) -> Result<Self, Error> {
        ensure_not_empty_with_context(&api_version, || {
            ErrorKind::InvalidApiVersion(api_version.clone())
        })?;

        let client = Client {
            inner: Arc::new(inner),
            token_source,
            api_version,
            host_name,
            user_agent: None,
        };

        Ok(client)
    }

    pub fn with_token_source(mut self, source: T) -> Self {
        self.token_source = Some(source);
        self
    }

    pub fn with_user_agent(mut self, user_agent: &str) -> Self {
        self.user_agent = Some(user_agent.to_string());
        self
    }

    pub fn inner(&self) -> &C {
        &self.inner
    }

    pub fn api_version(&self) -> &str {
        &self.api_version
    }

    pub fn user_agent(&self) -> Option<&str> {
        self.user_agent.as_ref().map(AsRef::as_ref)
    }

    pub fn host_name(&self) -> &Url {
        &self.host_name
    }

    fn add_sas_token(&self, req: &mut Request<Body>, path: &str) -> Result<(), Error> {
        if let Some(ref source) = self.token_source {
            let token_duration = Duration::hours(1);
            let expiry = Utc::now() + token_duration;
            let token = source.get(&expiry).context(ErrorKind::TokenSource)?;
            debug!(
                "Success generating token for request {} {}",
                req.method(),
                path,
            );
            req.headers_mut().append(
                http::header::AUTHORIZATION,
                format!("SharedAccessSignature {}", token).parse().unwrap(),
            );
        } else {
            debug!("Empty token source for request {} {}", req.method(), path);
        }

        Ok(())
    }

    pub fn request<BodyT, ResponseT>(
        &self,
        method: Method,
        path: &str,
        query: Option<HashMap<&str, &str>>,
        body: Option<BodyT>,
        add_if_match: bool,
    ) -> impl Future<Item = Option<ResponseT>, Error = Error>
    where
        BodyT: Serialize,
        ResponseT: 'static + DeserializeOwned,
    {
        // append api-version to the query string and url encode it
        let query = query
            .unwrap_or_else(HashMap::new)
            .iter()
            .fold(
                UrlSerializer::new(String::new()).append_pair("api-version", &self.api_version),
                |ser, (key, val)| ser.append_pair(key, val),
            )
            .finish();

        // build the full url
        let path_query = format!("{}?{}", path, query);
        self.host_name
            .join(&path_query)
            .with_context(|_| ErrorKind::UrlJoin(self.host_name.clone(), path_query))
            .context(ErrorKind::Http)
            .map_err(Error::from)
            .and_then(|url| {
                let mut req = Request::builder();
                req.method(method).uri(url.as_str());

                // add user agent header
                if let Some(ref user_agent) = self.user_agent {
                    req.header(http::header::USER_AGENT, &**user_agent);
                }

                // add an `If-Match: "*"` header if we've been asked to
                if add_if_match {
                    req.header(http::header::IF_MATCH, "*");
                }

                // add request body if there is any
                let mut req = if let Some(body) = body {
                    let serialized = serde_json::to_string(&body).context(ErrorKind::Http)?;
                    let serialized_len = serialized.len();
                    let mut req = req.body(Body::from(serialized)).context(ErrorKind::Http)?;
                    req.headers_mut()
                        .typed_insert(&ContentType(mime::APPLICATION_JSON));
                    req.headers_mut()
                        .typed_insert(&ContentLength(serialized_len as u64));
                    req
                } else {
                    req.body(Body::empty()).context(ErrorKind::Http)?
                };

                // add sas token
                self.add_sas_token(&mut req, path)?;

                Ok(req)
            })
            .map(|req| {
                self.inner
                    .call(req)
                    .then(|resp| resp.context(ErrorKind::Http).map_err(Error::from))
                    .and_then(|resp| {
                        let (http::response::Parts { status, .. }, body) = resp.into_parts();
                        body.concat2().then(move |res| {
                            let body = res.context(ErrorKind::Http)?;
                            Ok((status, body))
                        })
                    })
                    .and_then(|(status, body)| {
                        if status.is_success() {
                            Ok(body)
                        } else {
                            Err(Error::http_with_error_response(status, &*body))
                        }
                    })
                    .and_then(|body| {
                        if body.len() == 0 {
                            Ok(None)
                        } else {
                            Ok(Some(
                                serde_json::from_slice::<ResponseT>(&body)
                                    .context(ErrorKind::Http)?,
                            ))
                        }
                    })
            })
            .into_future()
            .flatten()
    }
}

impl<C, T> Clone for Client<C, T>
where
    T: TokenSource + Clone,
{
    fn clone(&self) -> Self {
        Client {
            inner: self.inner.clone(),
            token_source: self.token_source.clone(),
            api_version: self.api_version.clone(),
            host_name: self.host_name.clone(),
            user_agent: self.user_agent.clone(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashMap;
    use std::str;

    use chrono::{DateTime, Utc};
    use futures::future;
    use hyper::{Client as HyperClient, Request, Response};
    use tokio;
    use typed_headers::{mime, ContentType};
    use url::form_urlencoded::parse as parse_query;

    use crate::error::ErrorKind;

    struct StaticTokenSource {
        token: String,
    }

    impl StaticTokenSource {
        pub fn new(token: String) -> Self {
            StaticTokenSource { token }
        }
    }

    impl TokenSource for StaticTokenSource {
        type Error = Error;
        fn get(&self, _expiry: &DateTime<Utc>) -> Result<String, Error> {
            Ok(self.token.clone())
        }
    }

    impl Clone for StaticTokenSource {
        fn clone(&self) -> Self {
            StaticTokenSource {
                token: self.token.clone(),
            }
        }
    }

    #[test]
    fn empty_api_version_fails() {
        let hyper_client = HyperClient::new();
        let token_source: Option<StaticTokenSource> = None;
        let api_version = "".to_string();
        let client = Client::new(
            hyper_client,
            token_source,
            api_version.clone(),
            Url::parse("http://localhost").unwrap(),
        );
        match client {
            Ok(_) => panic!("Expected error but got a result."),
            Err(err) => {
                if let ErrorKind::InvalidApiVersion(s) = err.kind() {
                    assert_eq!(s, &api_version);
                } else {
                    panic!(
                        "Wrong error kind. Expected `InvalidApiVersion` found {:?}",
                        err
                    );
                }
            }
        }
    }

    #[test]
    fn white_space_api_version_fails() {
        let hyper_client = HyperClient::new();
        let token_source: Option<StaticTokenSource> = None;
        let api_version = "      ".to_string();
        let client = Client::new(
            hyper_client,
            token_source,
            api_version.clone(),
            Url::parse("http://localhost").unwrap(),
        );
        match client {
            Ok(_) => panic!("Expected error but got a result."),
            Err(err) => {
                if let ErrorKind::InvalidApiVersion(s) = err.kind() {
                    assert_eq!(s, &api_version);
                } else {
                    panic!(
                        "Wrong error kind. Expected `InvalidApiVersion` found {:?}",
                        err
                    );
                }
            }
        }
    }

    #[test]
    fn request_adds_api_version() {
        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#;
        let token_source: Option<StaticTokenSource> = None;

        let api_version2 = api_version.clone();
        let handler = move |req: Request<Body>| {
            assert_eq!(req.uri().path(), "/boo");
            assert_eq!(None, req.headers().get(hyper::header::IF_MATCH));

            // check that the query has api version_
            let query_map: HashMap<String, String> =
                parse_query(req.uri().query().unwrap().as_bytes())
                    .into_owned()
                    .collect();
            assert_eq!(query_map.get("api-version"), Some(&api_version2));

            future::ok(Response::new(response.into()))
        };
        let client = Client::new(handler, token_source, api_version, host_name).unwrap();

        let task = client.request::<String, String>(Method::GET, "/boo", None, None, false);

        let _result: Option<String> = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn request_adds_api_version_with_other_query_params() {
        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#;
        let token_source: Option<StaticTokenSource> = None;

        let api_version2 = api_version.clone();
        let handler = move |req: Request<Body>| {
            assert_eq!(req.uri().path(), "/boo");
            assert_eq!(None, req.headers().get(hyper::header::IF_MATCH));

            // check that the query has api version
            let query_map: HashMap<String, String> =
                parse_query(req.uri().query().unwrap().as_bytes())
                    .into_owned()
                    .collect();
            assert_eq!(query_map.get("api-version"), Some(&api_version2));
            assert_eq!(query_map.get("k1"), Some(&"v1".to_string()));
            assert_eq!(
                query_map.get("k2"),
                Some(&"this value has spaces and \u{1f42e}\u{1f42e}\u{1f42e}".to_string())
            );

            Ok(Response::new(response.into()))
        };
        let client = Client::new(handler, token_source, api_version, host_name).unwrap();

        let mut query = HashMap::new();
        query.insert("k1", "v1");
        query.insert(
            "k2",
            "this value has spaces and \u{1f42e}\u{1f42e}\u{1f42e}",
        );

        let task = client.request::<String, String>(Method::GET, "/boo", Some(query), None, false);

        let _result: String = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap()
            .unwrap();
    }

    #[test]
    fn request_adds_user_agent() {
        let api_version = "2018-04-10".to_string();
        let user_agent = "edgelet/request/test";
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#;
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request<Body>| {
            assert_eq!(
                user_agent,
                &*req.headers().get(hyper::header::USER_AGENT).unwrap(),
            );
            assert_eq!(None, req.headers().get(hyper::header::IF_MATCH));

            Ok(Response::new(response.into()))
        };
        let client = Client::new(handler, token_source, api_version, host_name)
            .unwrap()
            .with_user_agent(user_agent);

        let task = client.request::<String, String>(Method::GET, "/boo", None, None, false);

        let _result: String = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap()
            .unwrap();
    }

    #[test]
    fn request_adds_sas_token() {
        let api_version = "2018-04-10".to_string();
        let sas_token = "super_secret_password_y'all";
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#;

        let handler = move |req: Request<Body>| {
            let sas_header = req.headers().get(hyper::header::AUTHORIZATION).unwrap();
            let expected_sas = format!("SharedAccessSignature {}", sas_token);
            assert_eq!(expected_sas, *sas_header);
            assert_eq!(None, req.headers().get(hyper::header::IF_MATCH));

            Ok(Response::new(response.into()))
        };
        let token_source: Option<StaticTokenSource> =
            Some(StaticTokenSource::new(sas_token.to_string()));
        let client = Client::new(handler, token_source, api_version, host_name).unwrap();

        let task = client.request::<String, String>(Method::GET, "/boo", None, None, false);

        let _result: String = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap()
            .unwrap();
    }

    #[test]
    fn request_adds_if_match_header() {
        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#;
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request<Body>| {
            assert_eq!(
                Some("*").map(AsRef::as_ref),
                req.headers()
                    .get(hyper::header::IF_MATCH)
                    .map(AsRef::as_ref)
            );

            let mut response = Response::new(response.into());
            response
                .headers_mut()
                .typed_insert(&ContentType(mime::APPLICATION_JSON));
            Ok(response)
        };
        let client = Client::new(handler, token_source, api_version, host_name).unwrap();

        let task = client.request::<String, _>(Method::GET, "/boo", None, None, true);

        let _result: String = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap()
            .unwrap();
    }

    #[test]
    fn request_adds_body() {
        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#;
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request<Body>| {
            assert_eq!(None, req.headers().get(hyper::header::IF_MATCH));

            req.into_body()
                .concat2()
                .and_then(|req_body| {
                    str::from_utf8(&req_body)
                        .map(move |req_body| {
                            assert_eq!("\"Here be dragons\"".to_string(), req_body)
                        })
                        .map_err(|e| panic!("Error: {:?}", e))
                })
                .and_then(move |_| Ok(Response::new(response.into())))
        };
        let client = Client::new(handler, token_source, api_version, host_name).unwrap();

        let task = client.request::<String, String>(
            Method::POST,
            "/boo",
            None,
            Some("Here be dragons".to_string()),
            false,
        );

        let _result: String = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap()
            .unwrap();
    }

    #[test]
    fn request_can_return_empty_response() {
        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request<Body>| {
            assert_eq!(None, req.headers().get(hyper::header::IF_MATCH));

            req.into_body()
                .concat2()
                .and_then(|req_body| {
                    str::from_utf8(&req_body)
                        .map(move |req_body| {
                            assert_eq!("\"Here be dragons\"".to_string(), req_body)
                        })
                        .map_err(|e| panic!("Error: {:?}", e))
                })
                .and_then(|_| Ok(Response::new(Body::empty())))
        };
        let client = Client::new(handler, token_source, api_version, host_name).unwrap();

        let task = client.request::<String, _>(
            Method::POST,
            "/boo",
            None,
            Some("Here be dragons".to_string()),
            false,
        );

        let result: Option<String> = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
        assert_eq!(result, None);
    }

    #[test]
    fn request_returns_response() {
        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#;
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request<Body>| {
            assert_eq!(None, req.headers().get(hyper::header::IF_MATCH));

            Ok(Response::new(response.into()))
        };
        let client = Client::new(handler, token_source, api_version, host_name).unwrap();

        let task = client.request::<String, String>(Method::GET, "/boo", None, None, false);

        let result: String = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap()
            .unwrap();
        assert_eq!(result, "response");
    }
}
