// Copyright (c) Microsoft. All rights reserved.

use std::convert::AsRef;

use failure::{Fail, ResultExt};
use futures::{Future, IntoFuture};
use hyper::client::connect::Connect;
use hyper::client::HttpConnector;
use hyper::header::HeaderValue;
use hyper::{header, Body, Client as HyperClient, Request, Response, Uri};
use hyper_tls::HttpsConnector;
use log::info;
use url::percent_encoding::percent_decode;
use url::Url;

use crate::proxy::{Config, TokenSource};
use crate::{Error, ErrorKind};

#[derive(Clone)]
pub struct Client<T, S>
where
    T: TokenSource,
{
    config: Config<T>,
    client: S,
}

impl<T> Client<T, HyperHttpClient<HttpsConnector<HttpConnector>>>
where
    T: TokenSource,
{
    pub fn new(config: Config<T>) -> Self {
        // NOTE: We are defaulting to using 4 threads here. Is this a good default?
        //       This is what the "hyper" crate uses by default at this time.
        let mut http = HttpConnector::new(4);
        // if we don't do this then the HttpConnector rejects the "https" scheme
        http.enforce_http(false);

        let https = HttpsConnector::from((http, config.tls().clone()));
        let client = HyperHttpClient(HyperClient::builder().build(https));

        Client::with_client(client, config)
    }
}

impl<T, S> Client<T, S>
where
    T: TokenSource,
{
    pub fn with_client(client: S, config: Config<T>) -> Self {
        Client { config, client }
    }
}

impl<T, S> Client<T, S>
where
    T: TokenSource,
    S: HttpClient,
{
    pub fn request(
        &self,
        mut req: Request<Body>,
    ) -> impl Future<Item = Response<Body>, Error = Error> {
        build_uri(self.config.host().clone(), req.uri())
            .and_then(|url| {
                // set a full URL to redirect request to
                *req.uri_mut() = url;

                // set host value in request header
                if let Ok(host) = req.uri().host().unwrap_or_default().parse() {
                    req.headers_mut().insert(header::HOST, host);
                }

                // add authorization header with bearer token to authenticate request
                if let Some(token) = self.config.token().get() {
                    let token = HeaderValue::from_str(format!("Bearer {}", token).as_str())
                        .with_context(|_| ErrorKind::HeaderValue("Authorization".to_owned()))?;

                    req.headers_mut().insert(header::AUTHORIZATION, token);
                }

                Ok(req)
            })
            .map(|req| self.client.request(req))
            .into_future()
            .flatten()
    }
}

fn build_uri(base_url: Url, requested_uri: &Uri) -> Result<Uri, Error> {
    let path = percent_decode(requested_uri.path().as_bytes())
        .decode_utf8()
        .with_context(|_| ErrorKind::Uri(requested_uri.to_string()))?;

    let query = requested_uri
        .query()
        .map(|query| {
            percent_decode(query.as_bytes())
                .decode_utf8()
                .with_context(|_| ErrorKind::Uri(requested_uri.to_string()))
        })
        .transpose()?;

    let mut destination_url = base_url;
    destination_url.set_path(&path);
    destination_url.set_query(query.as_ref().map(AsRef::as_ref));

    let full_uri = destination_url
        .as_str()
        .parse::<Uri>()
        .with_context(|_| ErrorKind::Uri(destination_url.to_string()))?;
    Ok(full_uri)
}

pub struct HyperHttpClient<C>(HyperClient<C>);

impl<C> HttpClient for HyperHttpClient<C>
where
    C: Connect + Sync + 'static,
{
    fn request(&self, req: Request<Body>) -> ResponseFuture {
        let request = format!("{} {} {:?}", req.method(), req.uri(), req.version());

        let fut = self
            .0
            .request(req)
            .map_err({
                let request = request.clone();
                |err| Error::from(err.context(ErrorKind::HttpRequest(request)))
            })
            .map(move |res| {
                let body_length = res
                    .headers()
                    .get(header::CONTENT_LENGTH)
                    .and_then(|length| length.to_str().ok().map(ToString::to_string))
                    .unwrap_or_else(|| "-".to_string());

                info!("\"{}\" {} {}", request, res.status(), body_length);

                res
            });

        Box::new(fut)
    }
}

pub type ResponseFuture = Box<dyn Future<Item = Response<Body>, Error = Error> + Send>;

pub trait HttpClient {
    fn request(&self, req: Request<Body>) -> ResponseFuture;
}

#[cfg(test)]
mod tests {
    use futures::{Future, Stream};
    use hyper::{Body, Request, Response, Uri};
    use native_tls::TlsConnector;
    use tokio::runtime::current_thread;
    use url::Url;

    use crate::proxy::client::build_uri;
    use crate::proxy::config::ValueToken;
    use crate::proxy::test::config::config;
    use crate::proxy::test::http::client_fn;
    use crate::proxy::{Client, Config};
    use crate::{Error, ErrorKind};

    #[test]
    fn it_redirects_req_to_server() {
        let http = client_fn(|_| Ok(Response::new("This Is Fine".into())));
        let client = Client::with_client(http, config());
        let req = Request::new(Body::empty());

        let task = client.request(req).and_then(|res| {
            let status = res.status();
            res.into_body()
                .map_err(|_| Error::from(ErrorKind::Generic))
                .concat2()
                .map(move |body| (status, body.into_bytes()))
        });

        let res = current_thread::block_on_all(task).unwrap();
        let (_, body) = res;
        assert_eq!(body.as_ref(), b"This Is Fine");
    }

    #[test]
    fn it_handles_req_uri() {
        let http = client_fn(|req| {
            let uri = "https://iotedged:8080/api/values?version=v1"
                .parse::<Uri>()
                .unwrap();
            assert_eq!(req.uri(), &uri);

            Ok(Response::new("This Is Fine".into()))
        });
        let client = Client::with_client(http, config());
        let mut req = Request::new(Body::empty());
        *req.uri_mut() = "http://localhost:3000/api/values?version=v1"
            .parse()
            .unwrap();

        let task = client.request(req);

        current_thread::block_on_all(task).unwrap();
    }

    #[test]
    fn it_fails_when_token_is_invalid() {
        let config = Config::new(
            Url::parse("https://iotedged:8080").unwrap(),
            ValueToken(Some(String::from_utf8(vec![10]).unwrap())),
            TlsConnector::builder().build().unwrap(),
        );
        let http = client_fn(|_| Ok(Response::new("This Is Fine".into())));
        let client = Client::with_client(http, config);
        let req = Request::new(Body::empty());

        let task = client.request(req);

        let err = current_thread::block_on_all(task).unwrap_err();
        assert_eq!(
            err.kind(),
            &ErrorKind::HeaderValue("Authorization".to_owned())
        );
    }

    #[test]
    fn it_fails_when_http_client_returns_error() {
        let http = client_fn(|_| {
            Err(Error::from(ErrorKind::HttpRequest(
                "GET / HTTP 1.1".to_string(),
            )))
        });
        let client = Client::with_client(http, config());
        let req = Request::new(Body::empty());

        let task = client.request(req);

        let err = current_thread::block_on_all(task).unwrap_err();
        assert_eq!(
            err.kind(),
            &ErrorKind::HttpRequest("GET / HTTP 1.1".to_string(),)
        );
    }

    #[test]
    fn it_constructs_destination_uri() {
        let dst = "https://iotedged:8080/".parse().unwrap();
        let uri = "http://localhost//api/values?version=v1".parse().unwrap();

        let full_url = build_uri(dst, &uri).unwrap();

        let expected_url = "https://iotedged:8080//api/values?version=v1"
            .parse::<Uri>()
            .unwrap();
        assert_eq!(full_url, expected_url);
    }
}
