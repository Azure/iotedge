// Copyright (c) Microsoft. All rights reserved.

mod client;
mod config;
mod service;

pub use self::config::{get_config, Config, TokenSource};
pub use client::{Client, HttpClient};
pub use service::ProxyService;

#[cfg(test)]
mod test {
    pub(crate) mod http {
        use futures::IntoFuture;
        use hyper::{Body, Request, Response};

        use crate::proxy::client::ResponseFuture;
        use crate::proxy::HttpClient;
        use crate::Error;

        pub fn client_fn<F, S>(f: F) -> HttpClientFn<F>
        where
            F: Fn(Request<Body>) -> S,
            S: IntoFuture,
        {
            HttpClientFn { f }
        }

        pub struct HttpClientFn<F> {
            f: F,
        }

        impl<F, Ret> HttpClient for HttpClientFn<F>
        where
            F: Fn(Request<Body>) -> Ret,
            Ret: IntoFuture<Item = Response<Body>, Error = Error>,
            Ret::Future: Send + 'static,
        {
            fn request(&self, req: Request<Body>) -> ResponseFuture {
                Box::new(((self.f)(req)).into_future())
            }
        }
    }

    pub(crate) mod config {
        use native_tls::TlsConnector;
        use url::Url;

        use crate::proxy::config::ValueToken;
        use crate::proxy::Config;

        pub fn config() -> Config<ValueToken> {
            Config::new(
                Url::parse("https://iotedged:8080").unwrap(),
                ValueToken(None),
                TlsConnector::builder().build().unwrap(),
            )
        }
    }
}
