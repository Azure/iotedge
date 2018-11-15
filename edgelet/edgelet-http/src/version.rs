// Copyright (c) Microsoft. All rights reserved.

use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Request, Response};
use url::form_urlencoded::parse as parse_query;

use error::{Error, ErrorKind};
use IntoResponse;

pub const API_VERSION: &str = "2018-06-28";

#[derive(Clone)]
pub struct ApiVersionService<T> {
    upstream: T,
}

impl<T> ApiVersionService<T> {
    pub fn new(upstream: T) -> Self {
        ApiVersionService { upstream }
    }
}

impl<T> Service for ApiVersionService<T>
where
    T: Service<ResBody = Body>,
    <T as Service>::Future: Send + 'static,
    <T as Service>::Error: IntoResponse + Send + 'static,
{
    type ReqBody = T::ReqBody;
    type ResBody = T::ResBody;
    type Error = T::Error;
    type Future = Box<Future<Item = Response<Self::ResBody>, Error = Self::Error> + Send>;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let response = {
            let query = req.uri().query();
            let api_version =
                query
                .and_then(|query| {
                    let mut query = parse_query(query.as_bytes());
                    let (_, api_version) = query.find(|&(ref key, _)| key == "api-version")?;
                    Some(api_version)
                });

            match api_version {
                Some(ref api_version) if api_version == API_VERSION => Ok(()),
                Some(api_version) => Err(ErrorKind::InvalidApiVersion(api_version.into_owned())),
                None => Err(ErrorKind::InvalidApiVersion(String::new())),
            }
        };

        match response {
            Ok(()) => Box::new(self.upstream
                    .call(req)
                    .or_else(|e| future::ok(e.into_response()))),
            Err(kind) => Box::new(future::ok(Error::from(kind).into_response())),
        }
    }
}

impl<T> NewService for ApiVersionService<T>
where
    T: NewService,
    <T as NewService>::Future: Send + 'static,
    ApiVersionService<<T as NewService>::Service>: Service,
{
    type ReqBody = <ApiVersionService<<T as NewService>::Service> as Service>::ReqBody;
    type ResBody = <ApiVersionService<<T as NewService>::Service> as Service>::ResBody;
    type Error = <ApiVersionService<<T as NewService>::Service> as Service>::Error;
    type Service = ApiVersionService<<T as NewService>::Service>;
    type Future = Box<Future<Item = Self::Service, Error = Self::InitError> + Send>;
    type InitError = <T as NewService>::InitError;

    fn new_service(&self) -> Self::Future {
        Box::new(self.upstream.new_service().map(|upstream| ApiVersionService {
            upstream,
        }))
    }
}

#[cfg(test)]
mod tests {
    use failure::{Compat, Fail};
    use futures::future::FutureResult;
    use hyper::StatusCode;

    use super::*;

    #[derive(Clone)]
    struct TestService {
        status_code: StatusCode,
        error: bool,
    }

    impl Service for TestService {
        type ReqBody = Body;
        type ResBody = Body;
        type Error = Compat<Error>;
        type Future = FutureResult<Response<Self::ResBody>, Self::Error>;

        fn call(&mut self, _req: Request<Self::ReqBody>) -> Self::Future {
            if self.error {
                future::err(Error::from(ErrorKind::ServiceError).compat())
            } else {
                future::ok(
                    Response::builder()
                        .status(self.status_code)
                        .body(Body::default())
                        .unwrap(),
                )
            }
        }
    }

    #[test]
    fn api_version_check_succeeds() {
        let url = &format!("http://localhost?api-version={}", API_VERSION);
        let req = Request::get(url).body(Body::default()).unwrap();
        let mut api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::OK,
            error: false,
        });
        let response = Service::call(&mut api_service, req).wait().unwrap();
        assert_eq!(StatusCode::OK, response.status());
    }

    #[test]
    fn api_version_check_passes_status_code_through() {
        let url = &format!("http://localhost?api-version={}", API_VERSION);
        let req = Request::get(url).body(Body::default()).unwrap();
        let mut api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::IM_A_TEAPOT,
            error: false,
        });
        let response = Service::call(&mut api_service, req).wait().unwrap();
        assert_eq!(StatusCode::IM_A_TEAPOT, response.status());
    }

    #[test]
    fn api_version_check_returns_error_as_response() {
        let url = &format!("http://localhost?api-version={}", API_VERSION);
        let req = Request::get(url).body(Body::default()).unwrap();
        let mut api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::IM_A_TEAPOT,
            error: true,
        });
        let response = Service::call(&mut api_service, req).wait().unwrap();
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
    }

    #[test]
    fn api_version_does_not_exist() {
        let url = "http://localhost";
        let req = Request::get(url).body(Body::default()).unwrap();
        let mut api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::OK,
            error: false,
        });
        let response = Service::call(&mut api_service, req).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
    }

    #[test]
    fn api_version_is_unsupported() {
        let url = "http://localhost?api-version=not-a-valid-version";
        let req = Request::get(url).body(Body::default()).unwrap();
        let mut api_service = ApiVersionService::new(TestService {
            status_code: StatusCode::OK,
            error: false,
        });
        let response = Service::call(&mut api_service, req).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
    }
}
