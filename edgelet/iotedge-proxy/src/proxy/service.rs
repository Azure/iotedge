// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use failure::Compat;
use futures::future::FutureResult;
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Request, Response};
use log::debug;

use crate::proxy::{Client, HttpClient, TokenSource};
use crate::{logging, Error, IntoResponse};

pub struct ProxyService<T, S>
where
    T: TokenSource,
{
    client: Arc<Client<T, S>>,
}

impl<T, S> ProxyService<T, S>
where
    T: TokenSource,
{
    pub fn new(client: Client<T, S>) -> Self {
        ProxyService {
            client: Arc::new(client),
        }
    }
}

impl<T, S> Clone for ProxyService<T, S>
where
    T: TokenSource,
{
    fn clone(&self) -> Self {
        ProxyService {
            client: self.client.clone(),
        }
    }
}

impl<T, S> Service for ProxyService<T, S>
where
    T: TokenSource + 'static,
    S: HttpClient + 'static,
{
    type ReqBody = Body;
    type ResBody = Body;
    type Error = Compat<Error>;
    type Future = Box<dyn Future<Item = Response<Self::ResBody>, Error = Self::Error> + Send>;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let request = format!("{} {} {:?}", req.method(), req.uri(), req.version());
        debug!("Starting request processing {}", request);

        let fut = self.client.request(req).then(move |result| {
            let response = match result {
                Ok(response) => {
                    debug!("Finished request processing {}", request);
                    response
                }
                Err(err) => {
                    debug!("Finished request processing with error: {}", request);

                    logging::failure(&err);
                    err.into_response()
                }
            };

            Ok(response)
        });

        Box::new(fut)
    }
}

impl<T, S> NewService for ProxyService<T, S>
where
    T: TokenSource + 'static,
    S: HttpClient + 'static,
{
    type ReqBody = Body;
    type ResBody = Body;
    type Error = Compat<Error>;
    type Service = Self;
    type Future = FutureResult<Self::Service, Self::InitError>;
    type InitError = Compat<Error>;

    fn new_service(&self) -> Self::Future {
        future::ok(self.clone())
    }
}

#[cfg(test)]
mod tests {
    use failure::Compat;
    use futures::{Future, Stream};
    use hyper::service::Service;
    use hyper::{Body, Chunk, Request, Response, StatusCode};
    use serde_json::json;
    use tokio::runtime::current_thread::Runtime;

    use crate::proxy::test::config::config;
    use crate::proxy::test::http::client_fn;
    use crate::proxy::{Client, ProxyService};
    use crate::Error;
    use crate::ErrorKind;

    #[test]
    fn it_returns_response_to_caller() {
        let http = client_fn(|_| Ok(Response::new(Body::from("This Is Fine"))));
        let client = Client::with_client(http, config());
        let req = Request::new(Body::empty());
        let mut proxy = ProxyService::new(client);

        let task = proxy.call(req).map_err(Compat::into_inner).and_then(|res| {
            res.into_body()
                .concat2()
                .map(Chunk::into_bytes)
                .map_err(|_| Error::from(ErrorKind::Generic))
        });

        let mut runtime = Runtime::new().unwrap();
        let res = runtime.block_on(task).unwrap();
        assert_eq!(res.as_ref(), b"This Is Fine");
    }

    #[test]
    fn it_returns_500_with_error_message_on_err() {
        let http = client_fn(|_| Err(Error::from(ErrorKind::Generic)));
        let client = Client::with_client(http, config());
        let req = Request::new(Body::empty());
        let mut proxy = ProxyService::new(client);

        let task = proxy.call(req).map_err(Compat::into_inner).and_then(|res| {
            let status = res.status();
            res.into_body()
                .concat2()
                .map(move |body| {
                    (
                        status,
                        std::str::from_utf8(body.into_bytes().as_ref())
                            .unwrap()
                            .to_string(),
                    )
                })
                .map_err(|_| Error::from(ErrorKind::Generic))
        });

        let mut runtime = Runtime::new().unwrap();
        let res = runtime.block_on(task).unwrap();
        let (status, body) = res;
        assert_eq!(status, StatusCode::INTERNAL_SERVER_ERROR);
        assert_eq!(body.as_ref(), json!({ "message": "Error"}).to_string());
    }

    #[test]
    fn it_returns_502_with_error_message_on_server_unavailable_err() {
        let http = client_fn(|_| {
            Err(Error::from(ErrorKind::HttpRequest(
                "GET / HTTP/1.1".to_string(),
            )))
        });
        let client = Client::with_client(http, config());
        let req = Request::new(Body::empty());
        let mut proxy = ProxyService::new(client);

        let task = proxy.call(req).map_err(Compat::into_inner).and_then(|res| {
            let status = res.status();
            res.into_body()
                .concat2()
                .map(move |body| {
                    (
                        status,
                        std::str::from_utf8(body.into_bytes().as_ref())
                            .unwrap()
                            .to_string(),
                    )
                })
                .map_err(|_| Error::from(ErrorKind::Generic))
        });

        let mut runtime = Runtime::new().unwrap();
        let res = runtime.block_on(task).unwrap();
        let (status, body) = res;
        assert_eq!(status, StatusCode::BAD_GATEWAY);
        assert_eq!(
            body.as_ref(),
            json!({ "message": "Could not make an HTTP request: \"GET / HTTP/1.1\""}).to_string()
        );
    }
}
