// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::{Future, IntoFuture};
use hyper::client::connect::Connect;
use hyper::client::HttpConnector;
use hyper::header::HeaderValue;
use hyper::{header, Body, Client as HyperClient, Request, Response};
use hyper_tls::HttpsConnector;
use log::info;

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
        self.config
            .host()
            .join(req.uri().path_and_query().map_or("", |p| p.as_str()))
            .map_err(Error::from)
            .and_then(|url| {
                // set a full URL to redirect request to
                *req.uri_mut() = url.as_str().parse()?;

                // set host value in request header
                if let Ok(host) = req.uri().host().unwrap_or_default().parse() {
                    req.headers_mut().insert(header::HOST, host);
                }

                // add authorization header with bearer token to authenticate request
                if let Some(token) = self.config.token().get() {
                    let token = HeaderValue::from_str(format!("Bearer {}", token).as_str())
                        .context(ErrorKind::HeaderValue("Authorization".to_owned()))?;

                    req.headers_mut().insert(header::AUTHORIZATION, token);
                }

                Ok(req)
            })
            .map(|req| self.client.request(req))
            .into_future()
            .flatten()
    }
}

pub struct HyperHttpClient<C>(HyperClient<C>);

impl<C> HttpClient for HyperHttpClient<C>
where
    C: Connect + Sync + 'static,
{
    fn request(&self, req: Request<Body>) -> ResponseFuture {
        let request = format!("{} {} {:?}", req.method(), req.uri(), req.version());

        let fut = self.0.request(req).map_err(Error::from).map(move |res| {
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
                .map_err(Error::from)
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
        let http = client_fn(|_| Err(Error::from(ErrorKind::Hyper)));
        let client = Client::with_client(http, config());
        let req = Request::new(Body::empty());

        let task = client.request(req);

        let err = current_thread::block_on_all(task).unwrap_err();
        assert_eq!(err.kind(), &ErrorKind::Hyper);
    }
}
