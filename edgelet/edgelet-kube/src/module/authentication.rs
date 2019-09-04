// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;
use std::sync::{Arc, Mutex};

use failure::Fail;
use futures::future::{Either, IntoFuture};
use futures::{future, Future, Stream};
use hyper::service::Service;
use hyper::{Body, Request};
use log::Level;
use typed_headers::{Authorization, HeaderMapExt};

use edgelet_core::AuthId;
use edgelet_utils::log_failure;
use kube_client::{Client as KubeClient, TokenSource};

use crate::constants::EDGE_ORIGINAL_MODULEID;
use crate::error::Error;
use crate::{ErrorKind, KubeModuleRuntime};

pub fn authenticate<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    req: &Request<Body>,
) -> impl Future<Item = AuthId, Error = Error>
where
    T: TokenSource + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
    S::Future: Send,
{
    req.headers()
        .typed_get::<Authorization>()
        .map(|auth| {
            auth.and_then(|auth| {
                auth.as_bearer().map(|token| {
                    let client_copy = runtime.client();
                    let namespace = runtime.settings().namespace().to_owned();
                    let fut = runtime
                        .client()
                        .lock()
                        .expect("Unexpected lock error")
                        .borrow_mut()
                        .token_review(runtime.settings().namespace(), token.as_str())
                        .map_err(|err| {
                            log_failure(Level::Warn, &err);
                            Error::from(err.context(ErrorKind::Authentication))
                        })
                        .and_then(move |token_review| {
                            token_review
                                .status
                                .as_ref()
                                .filter(|status| status.authenticated.filter(|x| *x).is_some())
                                .and_then(|status| {
                                    status.user.as_ref().and_then(|user| user.username.clone())
                                })
                                .map_or(Either::A(future::ok(AuthId::None)), |name| {
                                    Either::B(get_module_original_name(
                                        &client_copy,
                                        &namespace,
                                        &name,
                                    ))
                                })
                        });

                    Either::A(fut)
                })
            })
            .unwrap_or_else(|| Either::B(future::ok(AuthId::None)))
        })
        .map_err(|err| err.context(ErrorKind::InvalidAuthToken))
        .into_future()
        .flatten()
}

fn get_module_original_name<T, S>(
    client: &Arc<Mutex<RefCell<KubeClient<T, S>>>>,
    namespace: &str,
    username: &str,
) -> impl Future<Item = AuthId, Error = Error>
where
    T: TokenSource + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
    S::Future: Send,
{
    match username.split(':').last() {
        Some(name) => Either::A({
            let name = name.to_owned();

            client
                .lock()
                .expect("Unexpected lock error")
                .borrow_mut()
                .get_service_account(namespace, &name)
                .map_err(|err| {
                    log_failure(Level::Warn, &err);
                    Error::from(err.context(ErrorKind::KubeClient))
                })
                .map(|service_account| {
                    let module_name = service_account
                        .metadata
                        .as_ref()
                        .and_then(|metadata| metadata.annotations.as_ref())
                        .and_then(|annotations| annotations.get(EDGE_ORIGINAL_MODULEID).cloned())
                        .unwrap_or(name);

                    AuthId::Value(module_name.into())
                })
        }),
        None => Either::B(future::ok(AuthId::None)),
    }
}

#[cfg(test)]
mod tests {
    use futures::future;
    use hyper::service::service_fn;
    use hyper::{header, Body, Method, Request, Response, StatusCode};
    use maplit::btreemap;
    use serde_json::json;
    use tokio::runtime::Runtime;
    use typed_headers::{mime, ContentLength, ContentType, HeaderMapExt};

    use edgelet_core::{AuthId, Authenticator};
    use edgelet_test_utils::routes;
    use edgelet_test_utils::web::{
        make_req_dispatcher, HttpMethod, RequestHandler, RequestPath, ResponseFuture,
    };

    use crate::tests::{create_runtime, make_settings, not_found_handler, response};
    use crate::ErrorKind;

    #[test]
    fn it_authenticates_with_none_when_no_auth_token_provided() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            POST "/apis/authentication.k8s.io/v1/tokenreviews" => unauthenticated_token_review_handler()
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let req = Request::default();

        let task = runtime.authenticate(&req);

        let mut runtime = Runtime::new().unwrap();
        let auth_id = runtime.block_on(task).unwrap();

        assert_eq!(auth_id, AuthId::None)
    }

    #[test]
    fn it_authenticates_with_none_when_invalid_auth_header_provided() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            POST "/apis/authentication.k8s.io/v1/tokenreviews" => unauthenticated_token_review_handler()
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let mut req = Request::default();
        req.headers_mut()
            .insert(header::AUTHORIZATION, "BeErer token".parse().unwrap());

        let task = runtime.authenticate(&req);

        let mut runtime = Runtime::new().unwrap();
        let auth_id = runtime.block_on(task).unwrap();

        assert_eq!(auth_id, AuthId::None)
    }

    #[test]
    fn it_authenticates_with_none_when_invalid_auth_token_provided() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            POST "/apis/authentication.k8s.io/v1/tokenreviews" => unauthenticated_token_review_handler()
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let mut req = Request::default();
        req.headers_mut().insert(
            header::AUTHORIZATION,
            "\u{3aa}\u{3a9}\u{3a4}".parse().unwrap(),
        );

        let task = runtime.authenticate(&req);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();

        assert_eq!(err.kind(), &ErrorKind::InvalidAuthToken);
    }

    #[test]
    fn it_authenticates_with_none_when_unknown_auth_token_provided() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            POST "/apis/authentication.k8s.io/v1/tokenreviews" => unauthenticated_token_review_handler()
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let mut req = Request::default();
        req.headers_mut().insert(
            header::AUTHORIZATION,
            "Bearer token-unknown".parse().unwrap(),
        );

        let task = runtime.authenticate(&req);

        let mut runtime = Runtime::new().unwrap();
        let auth_id = runtime.block_on(task).unwrap();

        assert_eq!(auth_id, AuthId::None)
    }

    #[test]
    fn it_authenticates_with_none_when_module_auth_token_provided_but_sa_does_not_exists() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            POST "/apis/authentication.k8s.io/v1/tokenreviews" => authenticated_token_review_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let mut req = Request::default();
        req.headers_mut()
            .insert(header::AUTHORIZATION, "Bearer token".parse().unwrap());

        let task = runtime.authenticate(&req);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();

        assert_eq!(err.kind(), &ErrorKind::KubeClient);
    }

    #[test]
    fn it_authenticates_with_sa_name_when_sa_does_not_contain_original_name() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            POST "/apis/authentication.k8s.io/v1/tokenreviews" => authenticated_token_review_handler(),
            GET format!("/api/v1/namespaces/{}/serviceaccounts/edgeagent", settings.namespace()) => get_service_account_without_annotations_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let mut req = Request::default();
        req.headers_mut()
            .insert(header::AUTHORIZATION, "Bearer token".parse().unwrap());

        let task = runtime.authenticate(&req);

        let mut runtime = Runtime::new().unwrap();
        let auth_id = runtime.block_on(task).unwrap();

        assert_eq!(auth_id, AuthId::Value("edgeagent".into()));
    }

    #[test]
    fn it_authenticates_with_original_name_when_module_auth_token_provided() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            POST "/apis/authentication.k8s.io/v1/tokenreviews" => authenticated_token_review_handler(),
            GET format!("/api/v1/namespaces/{}/serviceaccounts/edgeagent", settings.namespace()) => get_service_account_with_annotations_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let mut req = Request::default();
        req.headers_mut()
            .insert(header::AUTHORIZATION, "Bearer token".parse().unwrap());

        let task = runtime.authenticate(&req);

        let mut runtime = Runtime::new().unwrap();
        let auth_id = runtime.block_on(task).unwrap();

        assert_eq!(auth_id, AuthId::Value("$edgeAgent".into()));
    }

    fn authenticated_token_review_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        make_token_review_handler(|| {
            json!({
                "kind": "TokenReview",
                "spec": { "token": "token" },
                "status": {
                    "authenticated": true,
                    "user": {
                        "username": "system:serviceaccount:my-namespace:edgeagent"
                    }
                }}
            )
            .to_string()
        })
    }

    fn unauthenticated_token_review_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        make_token_review_handler(|| {
            json!({
                "kind": "TokenReview",
                "spec": { "token": "token" },
                "status": {
                    "authenticated": false,
                }}
            )
            .to_string()
        })
    }

    fn make_token_review_handler(
        on_token_review: impl Fn() -> String + Clone + Send + 'static,
    ) -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            let response = on_token_review();
            let response_len = response.len();

            let mut response = Response::new(response.into());
            response
                .headers_mut()
                .typed_insert(&ContentLength(response_len as u64));
            response
                .headers_mut()
                .typed_insert(&ContentType(mime::APPLICATION_JSON));
            Box::new(future::ok(response)) as ResponseFuture
        }
    }

    fn get_service_account_with_annotations_handler(
    ) -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "ServiceAccount",
                    "apiVersion": "v1",
                    "metadata": {
                        "name": "edgeagent",
                        "namespace": "my-namespace",
                        "annotations": {
                            "net.azure-devices.edge.original-moduleid": "$edgeAgent"
                        }
                    }
                })
                .to_string()
            })
        }
    }

    fn get_service_account_without_annotations_handler(
    ) -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "ServiceAccount",
                    "apiVersion": "v1",
                    "metadata": {
                        "name": "edgeagent",
                        "namespace": "my-namespace",
                    }
                })
                .to_string()
            })
        }
    }
}
