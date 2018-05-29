// Copyright (c) Microsoft. All rights reserved.

use std::io;

use futures::{future, Future};
use http::{Request, Response};
use hyper::server::{NewService, Service};
use hyper::{Body, Error as HyperError};
use url::form_urlencoded::parse as parse_query;

use error::{Error, ErrorKind};
use IntoResponse;

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
    fn into_response(self) -> Response<Body> {
        Error::from(self).into_response()
    }
}

impl<T> Service for ApiVersionService<T>
where
    T: Service<Request = Request<Body>, Response = Response<Body>, Error = HyperError>,
    T::Future: 'static,
{
    type Request = T::Request;
    type Response = T::Response;
    type Error = T::Error;
    type Future = Box<Future<Item = Self::Response, Error = Self::Error>>;

    fn call(&self, req: Self::Request) -> Self::Future {
        let response = req.uri()
            .query()
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
    T: Clone + Service<Request = Request<Body>, Response = Response<Body>, Error = HyperError>,
    T::Future: 'static,
{
    type Request = T::Request;
    type Response = Response<Body>;
    type Error = HyperError;
    type Instance = Self;

    fn new_service(&self) -> io::Result<Self::Instance> {
        Ok(self.clone())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use http::StatusCode;

    #[derive(Clone)]
    struct TestService {
        status_code: StatusCode,
        error: bool,
    }

    impl Service for TestService {
        type Request = Request<Body>;
        type Response = Response<Body>;
        type Error = HyperError;
        type Future = Box<Future<Item = Self::Response, Error = Self::Error>>;

        fn call(&self, _req: Self::Request) -> Self::Future {
            Box::new(if self.error {
                future::err(HyperError::TooLarge)
            } else {
                future::ok(
                    Response::builder()
                        .status(self.status_code)
                        .body(Body::default())
                        .unwrap(),
                )
            })
        }
    }

    #[test]
    fn api_version_check_succeeds() {
        let url = &format!("http://localhost?api-version={}", API_VERSION);
        let req = Request::get(url).body(Body::default()).unwrap();
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::OK,
            error: false,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::OK, response.status());
    }

    #[test]
    fn api_version_check_passes_status_code_through() {
        let url = &format!("http://localhost?api-version={}", API_VERSION);
        let req = Request::get(url).body(Body::default()).unwrap();
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::IM_A_TEAPOT,
            error: false,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::IM_A_TEAPOT, response.status());
    }

    #[test]
    fn api_version_check_returns_error_as_response() {
        let url = &format!("http://localhost?api-version={}", API_VERSION);
        let req = Request::get(url).body(Body::default()).unwrap();
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::IM_A_TEAPOT,
            error: true,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
    }

    #[test]
    fn api_version_does_not_exist() {
        let url = "http://localhost";
        let req = Request::get(url).body(Body::default()).unwrap();
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::OK,
            error: false,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
    }

    #[test]
    fn api_version_is_unsupported() {
        let url = "http://localhost?api-version=not-a-valid-version";
        let req = Request::get(url).body(Body::default()).unwrap();
        let api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::OK,
            error: false,
        });
        let response = Service::call(&api_service, req).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
    }
}
