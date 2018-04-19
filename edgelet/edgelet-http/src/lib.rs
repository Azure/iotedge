// Copyright (c) Microsoft. All rights reserved.

extern crate failure;
#[macro_use]
extern crate failure_derive;
extern crate futures;
extern crate hyper;
extern crate regex;
#[macro_use]
extern crate serde_json;
extern crate url;

use std::io;

use futures::{future, Future};
use hyper::Error as HyperError;
use hyper::server::{NewService, Request, Response, Service};
use url::form_urlencoded::parse as parse_query;

mod error;
pub mod route;

pub use error::{Error, ErrorKind};

pub trait IntoResponse {
    fn into_response(self) -> Response;
}

impl IntoResponse for Response {
    fn into_response(self) -> Response {
        self
    }
}

pub const API_VERSION: &str = "2018-06-28";

#[derive(Clone)]
pub struct ApiVersionService<T> {
    upstream: T,
}

impl<T> ApiVersionService<T> {
    pub fn new(upstream: T) -> ApiVersionService<T> {
        ApiVersionService { upstream }
    }
}

impl IntoResponse for HyperError {
    fn into_response(self) -> Response {
        Error::from(self).into_response()
    }
}

impl<T> Service for ApiVersionService<T>
where
    T: Service<Request = Request, Response = Response, Error = HyperError>,
    T::Future: 'static,
{
    type Request = T::Request;
    type Response = T::Response;
    type Error = T::Error;
    type Future = Box<Future<Item = Self::Response, Error = Self::Error>>;

    fn call(&self, req: Self::Request) -> Self::Future {
        let response = req.query()
            .map(|query| query.to_owned())
            .and_then(|query| {
                parse_query(query.as_bytes())
                    .find(|&(ref key, _)| key == "api-version")
                    .and_then(|(_, v)| if v == API_VERSION { Some(()) } else { None })
                    .map(|_| {
                        future::Either::A(
                            self.upstream
                                .call(req)
                                .or_else(|e| future::ok(e.into_response())),
                        )
                    })
            })
            .unwrap_or_else(|| {
                let err = Error::from(ErrorKind::InvalidApiVersion);
                future::Either::B(future::ok(err.into_response()))
            });

        Box::new(response)
    }
}

impl<T> NewService for ApiVersionService<T>
where
    T: Clone + Service<Request = Request, Response = Response, Error = HyperError>,
    T::Future: 'static,
{
    type Request = T::Request;
    type Response = Response;
    type Error = HyperError;
    type Instance = Self;

    fn new_service(&self) -> io::Result<Self::Instance> {
        Ok(self.clone())
    }
}

#[cfg(test)]
mod tests {
    use hyper::{Method, StatusCode};

    use super::*;

    #[derive(Clone)]
    struct TestService {
        status_code: StatusCode,
        error: bool,
    }

    impl Service for TestService {
        type Request = Request;
        type Response = Response;
        type Error = HyperError;
        type Future = Box<Future<Item = Self::Response, Error = Self::Error>>;

        fn call(&self, _req: Self::Request) -> Self::Future {
            Box::new(if self.error {
                future::err(HyperError::TooLarge)
            } else {
                future::ok(Response::new().with_status(self.status_code))
            })
        }
    }

    #[test]
    fn api_version_check_succeeds() {
        let url = &format!("http://localhost?api-version={}", API_VERSION);
        let req = Request::new(Method::Get, url.parse().unwrap());
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::Ok,
            error: false,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::Ok, response.status());
    }

    #[test]
    fn api_version_check_passes_status_code_through() {
        let url = &format!("http://localhost?api-version={}", API_VERSION);
        let req = Request::new(Method::Get, url.parse().unwrap());
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::ImATeapot,
            error: false,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::ImATeapot, response.status());
    }

    #[test]
    fn api_version_check_returns_error_as_response() {
        let url = &format!("http://localhost?api-version={}", API_VERSION);
        let req = Request::new(Method::Get, url.parse().unwrap());
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::ImATeapot,
            error: true,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::InternalServerError, response.status());
    }

    #[test]
    fn api_version_does_not_exist() {
        let url = "http://localhost";
        let req = Request::new(Method::Get, url.parse().unwrap());
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::Ok,
            error: false,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::BadRequest, response.status());
    }

    #[test]
    fn api_version_is_unsupported() {
        let url = "http://localhost?api-version=not-a-valid-version";
        let req = Request::new(Method::Get, url.parse().unwrap());
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::Ok,
            error: false,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::BadRequest, response.status());
    }
}
