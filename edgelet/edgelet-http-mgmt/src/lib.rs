// Copyright (c) Microsoft. All rights reserved.

mod system_info;

#[derive(Clone)]
pub struct Service<M>
where
    M: edgelet_core::ModuleRuntime,
{
    pub runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

#[macro_export]
macro_rules! make_service {
    (
        service: $service_ty:ty,
        { $($impl_generics:tt)* }
        { $($bounds:tt)* }
        api_version: $api_version_ty:ty,
        routes: [
            $($route:path ,)*
        ],
    ) => {
        impl $($impl_generics)* hyper::service::Service<hyper::Request<hyper::Body>> for $service_ty where $($bounds)* {
            type Response = hyper::Response<hyper::Body>;
            type Error = std::convert::Infallible;
            type Future = std::pin::Pin<Box<dyn std::future::Future<Output = Result<Self::Response, Self::Error>> + Send>>;

            fn poll_ready(&mut self, _cx: &mut std::task::Context<'_>) -> std::task::Poll<Result<(), Self::Error>> {
                std::task::Poll::Ready(Ok(()))
            }

            fn call(&mut self, req: hyper::Request<hyper::Body>) -> Self::Future {
                fn call_inner $($impl_generics)* (
                    this: &mut $service_ty,
                    req: hyper::Request<hyper::Body>,
                ) -> std::pin::Pin<Box<dyn std::future::Future<Output = Result<hyper::Response<hyper::Body>, std::convert::Infallible>> + Send>> where $($bounds)* {
                    const HYPER_REQUEST_TIMEOUT: std::time::Duration = std::time::Duration::from_secs(5);
                    let (http::request::Parts { method, uri, headers, extensions, .. }, body) = req.into_parts();

                    let path = uri.path();

                    let (api_version, query_params) = {
                        let mut api_version = None;
                        let mut query_params = vec![];

                        if let Some(query) = uri.query() {
                            let mut params = url::form_urlencoded::parse(query.as_bytes());
                            while let Some((name, value)) = params.next() {
                                if name == "api-version" {
                                    api_version = Some(value);
                                }
                                else {
                                    query_params.push((name, value));
                                }
                            }
                        }

                        let api_version = match api_version {
                            Some(api_version) => api_version,
                            None => return Box::pin(futures_util::future::ok((http_common::server::Error {
                                status_code: http::StatusCode::BAD_REQUEST,
                                message: "api-version not specified".into(),
                            }).to_http_response())),
                        };
                        let api_version: $api_version_ty = match api_version.parse() {
                            Ok(api_version) => api_version,
                            Err(()) => return Box::pin(futures_util::future::ok((http_common::server::Error {
                                status_code: http::StatusCode::BAD_REQUEST,
                                message: format!("invalid api-version {:?}", api_version).into(),
                            }).to_http_response())),
                        };
                        (api_version, query_params)
                    };

                    $(
                        let route_api_version_matches = <$route as http_common::server::Route>::api_version().contains(&api_version);
                        if route_api_version_matches {
                        let route: Option<$route> = http_common::server::Route::from_uri(&*this, path, &query_params, &extensions);
                            if let Some(route) = route {
                                return Box::pin(async move {
                                    let response = match method {
                                        http::Method::DELETE => {
                                            let body = {
                                                let content_type = headers.get(hyper::header::CONTENT_TYPE).and_then(|value| value.to_str().ok());
                                                if content_type.as_deref() == Some("application/json") {
                                                    let body = match tokio::time::timeout(HYPER_REQUEST_TIMEOUT, hyper::body::to_bytes(body)).await {
                                                        Ok(result) => match result {
                                                            Ok(body) => body,
                                                            Err(err) => return Ok((http_common::server::Error {
                                                                status_code: http::StatusCode::BAD_REQUEST,
                                                                message: http_common::server::error_to_message(&err).into(),
                                                            }).to_http_response()),
                                                        },
                                                        Err(timeout_err) => return Ok((http_common::server::Error {
                                                            status_code: http::StatusCode::REQUEST_TIMEOUT,
                                                            message: http_common::server::error_to_message(&timeout_err).into(),
                                                        }).to_http_response()),
                                                    };

                                                    let body: <$route as http_common::server::Route>::DeleteBody = match serde_json::from_slice(&body) {
                                                        Ok(body) => body,
                                                        Err(err) => return Ok((http_common::server::Error {
                                                            status_code: http::StatusCode::UNPROCESSABLE_ENTITY,
                                                            message: http_common::server::error_to_message(&err).into(),
                                                        }).to_http_response()),
                                                    };

                                                    Some(body)
                                                }
                                                else {
                                                    None
                                                }
                                            };

                                            let (status_code, response) = match <$route as http_common::server::Route>::delete(route, body).await {
                                                Ok(result) => result,
                                                Err(err) => return Ok(err.to_http_response()),
                                            };
                                            http_common::server::json_response(status_code, response.as_ref())
                                        },

                                        http::Method::GET => {
                                            let (status_code, response) = match <$route as http_common::server::Route>::get(route).await {
                                                Ok(result) => result,
                                                Err(err) => return Ok(err.to_http_response()),
                                            };
                                            http_common::server::json_response(status_code, Some(&response))
                                        },

                                        http::Method::POST => {
                                            let body = {
                                                let content_type = headers.get(hyper::header::CONTENT_TYPE).and_then(|value| value.to_str().ok());
                                                if content_type.as_deref() == Some("application/json") {
                                                    let body = match tokio::time::timeout(HYPER_REQUEST_TIMEOUT, hyper::body::to_bytes(body)).await {
                                                        Ok(result) => match result {
                                                            Ok(body) => body,
                                                            Err(err) => return Ok((http_common::server::Error {
                                                                status_code: http::StatusCode::BAD_REQUEST,
                                                                message: http_common::server::error_to_message(&err).into(),
                                                            }).to_http_response()),
                                                        },
                                                        Err(timeout_err) => return Ok((http_common::server::Error {
                                                            status_code: http::StatusCode::REQUEST_TIMEOUT,
                                                            message: http_common::server::error_to_message(&timeout_err).into(),
                                                        }).to_http_response()),
                                                    };

                                                    let body: <$route as http_common::server::Route>::PostBody = match serde_json::from_slice(&body) {
                                                        Ok(body) => body,
                                                        Err(err) => return Ok((http_common::server::Error {
                                                            status_code: http::StatusCode::UNPROCESSABLE_ENTITY,
                                                            message: http_common::server::error_to_message(&err).into(),
                                                        }).to_http_response()),
                                                    };

                                                    Some(body)
                                                }
                                                else {
                                                    None
                                                }
                                            };

                                            let (status_code, response) = match <$route as http_common::server::Route>::post(route, body).await {
                                                Ok(result) => result,
                                                Err(err) => return Ok(err.to_http_response()),
                                            };
                                            http_common::server::json_response(status_code, response.as_ref())
                                        },

                                        http::Method::PUT => {
                                            let content_type = headers.get(hyper::header::CONTENT_TYPE).and_then(|value| value.to_str().ok());
                                            if content_type.as_deref() != Some("application/json") {
                                                return Ok((http_common::server::Error {
                                                    status_code: http::StatusCode::UNSUPPORTED_MEDIA_TYPE,
                                                    message: "request body must be application/json".into(),
                                                }).to_http_response());
                                            }

                                            let body = match tokio::time::timeout(HYPER_REQUEST_TIMEOUT, hyper::body::to_bytes(body)).await {
                                                Ok(result) => match result {
                                                    Ok(body) => body,
                                                    Err(err) => return Ok((http_common::server::Error {
                                                        status_code: http::StatusCode::BAD_REQUEST,
                                                        message: http_common::server::error_to_message(&err).into(),
                                                    }).to_http_response()),
                                                },
                                                Err(timeout_err) => return Ok((http_common::server::Error {
                                                    status_code: http::StatusCode::REQUEST_TIMEOUT,
                                                    message: http_common::server::error_to_message(&timeout_err).into(),
                                                }).to_http_response()),
                                            };

                                            let body: <$route as http_common::server::Route>::PutBody = match serde_json::from_slice(&body) {
                                                Ok(body) => body,
                                                Err(err) => return Ok((http_common::server::Error {
                                                    status_code: http::StatusCode::UNPROCESSABLE_ENTITY,
                                                    message: http_common::server::error_to_message(&err).into(),
                                                }).to_http_response()),
                                            };

                                            let (status_code, response) = match <$route as http_common::server::Route>::put(route, body).await {
                                                Ok(result) => result,
                                                Err(err) => return Ok(err.to_http_response()),
                                            };
                                            http_common::server::json_response(status_code, Some(&response))
                                        },

                                        _ => return Ok((http_common::server::Error {
                                            status_code: http::StatusCode::BAD_REQUEST,
                                            message: "method not allowed".into(),
                                        }).to_http_response()),
                                    };
                                    Ok(response)
                                })
                            }
                        }
                    )*

                    let res = (http_common::server::Error {
                        status_code: http::StatusCode::NOT_FOUND,
                        message: "not found".into(),
                    }).to_http_response();
                    Box::pin(futures_util::future::ok(res))
                }

                // TODO: When we get distributed tracing, associate these two logs with the tracing ID.
                log::info!("<-- {:?} {:?} {:?}", req.method(), req.uri(), req.headers());
                let res = call_inner(self, req);
                Box::pin(async move {
                    let res = res.await;
                    match &res {
                        Ok(res) => log::info!("--> {:?} {:?}", res.status(), res.headers()),
                        Err(err) => log::error!("-!> {:?}", err),
                    }
                    res
                })
            }
        }
    };
}

make_service! {
    service: Service<M>,
    { <M> }
    { M: edgelet_core::ModuleRuntime + Send + Sync + 'static }
    api_version: edgelet_http::ApiVersion,
    routes: [
        system_info::get::Route<M>,
    ],
}
