// Copyright (c) Microsoft. All rights reserved.

use std::cmp::Ordering;
use std::collections::BTreeMap;
use std::error::Error as StdError;
use std::fs;
use std::io;
#[cfg(unix)]
use std::os::unix::net::UnixListener as StdUnixListener;

use futures::prelude::*;
use hyper::body::Payload;
use hyper::server::conn::Http;
use hyper::service::service_fn;
use hyper::Error as HyperError;
use hyper::{self, Body, Method, Request, Response};
#[cfg(unix)]
use hyperlocal::server::{Http as UdsHttp, Incoming as UdsIncoming};

pub fn run_tcp_server<F, R>(
    ip: &str,
    handler: F,
) -> (impl Future<Item = (), Error = hyper::Error>, u16)
where
    F: 'static + Fn(Request<Body>) -> R + Clone + Send,
    R: 'static + Future<Item = Response<Body>, Error = hyper::Error> + Send,
{
    let addr = &format!("{}:0", ip).parse().unwrap();

    let serve = Http::new()
        .serve_addr(addr, move || service_fn(handler.clone()))
        .unwrap();
    let port = serve.incoming_ref().local_addr().port();
    let server = serve.for_each(|connecting| {
        connecting
            .then(|connection| {
                let connection = connection.unwrap();
                Ok::<_, hyper::Error>(connection)
            })
            .flatten()
    });
    (server, port)
}

pub fn run_uds_server<F, R>(path: &str, handler: F) -> impl Future<Item = (), Error = io::Error>
where
    F: 'static + Fn(Request<Body>) -> R + Clone + Send + Sync,
    R: 'static + Future<Item = Response<Body>, Error = io::Error> + Send,
{
    fs::remove_file(&path).unwrap_or(());

    // Bind a listener synchronously, so that the caller's client will not fail to connect
    // regardless of when the asynchronous server accepts the connection
    let listener = StdUnixListener::bind(path).unwrap();
    let incoming = UdsIncoming::from_std(listener, &Default::default()).unwrap();
    let serve = UdsHttp::new().serve_incoming(incoming, move || service_fn(handler.clone()));

    serve.for_each(|connecting| {
        connecting
            .then(|connection| {
                let connection = connection.unwrap();
                Ok::<_, hyper::Error>(connection)
            })
            .flatten()
            .map_err(|e| {
                io::Error::new(
                    io::ErrorKind::Other,
                    format!("failed to serve connection: {}", e),
                )
            })
    })
}

#[derive(Clone, PartialEq, Eq, Hash, Ord, PartialOrd)]
pub struct RequestPath(pub String);

#[derive(Clone, PartialEq, Eq, Hash)]
pub struct HttpMethod(pub Method);

impl Ord for HttpMethod {
    fn cmp(&self, other: &Self) -> Ordering {
        self.0.as_str().cmp(other.0.as_str())
    }
}

impl PartialOrd for HttpMethod {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.cmp(other))
    }
}

pub trait CloneableService: objekt::Clone {
    type ReqBody: Payload;
    type ResBody: Payload;
    type Error: Into<Box<dyn StdError + Send + Sync>>;
    type Future: Future<Item = Response<Self::ResBody>, Error = Self::Error>;

    fn call(&self, req: Request<Self::ReqBody>) -> Self::Future;
}

objekt::clone_trait_object!(CloneableService<
    ReqBody = Body,
    ResBody = Body,
    Error = HyperError,
    Future = ResponseFuture,
> + Send);

pub type ResponseFuture = Box<dyn Future<Item = Response<Body>, Error = HyperError> + Send>;
pub type RequestHandler = Box<
    dyn CloneableService<
            ReqBody = Body,
            ResBody = Body,
            Error = HyperError,
            Future = ResponseFuture,
        > + Send,
>;

impl<T, F> CloneableService for T
where
    T: Fn(Request<Body>) -> F + Clone,
    F: IntoFuture<Item = Response<Body>, Error = HyperError>,
{
    type ReqBody = Body;
    type ResBody = Body;
    type Error = F::Error;
    type Future = F::Future;

    fn call(&self, req: Request<Self::ReqBody>) -> Self::Future {
        (self)(req).into_future()
    }
}

pub fn make_req_dispatcher(
    dispatch_table: BTreeMap<(HttpMethod, RequestPath), RequestHandler>,
    default_handler: RequestHandler,
) -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
    move |req: Request<Body>| {
        let key = (
            HttpMethod(req.method().clone()),
            RequestPath(req.uri().path().to_string()),
        );
        let handler = dispatch_table.get(&key).unwrap_or(&default_handler);

        Box::new(handler.call(req))
    }
}

#[macro_export]
macro_rules! routes {
    ($($method:ident $path:expr => $handler:expr),+ $(,)*) => ({
        btreemap! {
            $((HttpMethod(Method::$method), RequestPath(From::from($path))) => Box::new($handler) as RequestHandler,)*
        }
    });
}
