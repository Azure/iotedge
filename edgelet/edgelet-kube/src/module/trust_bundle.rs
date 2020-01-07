// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;
use futures::future::Either;
use futures::{future, Future, IntoFuture, Stream};
use hyper::service::Service;
use hyper::Body;

use edgelet_core::GetTrustBundle;
use kube_client::TokenSource;

use crate::convert::trust_bundle_to_config_map;
use crate::{Error, ErrorKind, KubeModuleRuntime};

#[allow(clippy::needless_pass_by_value)]
pub fn init_trust_bundle<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    crypto: impl GetTrustBundle,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource,
    S: Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
{
    crypto
        .get_trust_bundle()
        .map_err(|err| Error::from(err.context(ErrorKind::IdentityCertificate)))
        .and_then(|cert| trust_bundle_to_config_map(runtime.settings(), &cert))
        .map(|(name, new_config_map)| {
            let client_copy = runtime.client();
            let namespace_copy = runtime.settings().namespace().to_owned();

            runtime
                .client()
                .lock()
                .expect("Unexpected lock error")
                .borrow_mut()
                .list_config_maps(
                    runtime.settings().namespace(),
                    Some(&name),
                    Some(&runtime.settings().device_hub_selector()),
                )
                .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
                .and_then(move |config_maps| {
                    if let Some(current) = config_maps.items.into_iter().find(|config_map| {
                        config_map.metadata.as_ref().map_or(false, |meta| {
                            meta.name.as_ref().map_or(false, |n| *n == name)
                        })
                    }) {
                        if current == new_config_map {
                            Either::A(Either::A(future::ok(())))
                        } else {
                            let fut = client_copy
                                .lock()
                                .expect("Unexpected lock error")
                                .borrow_mut()
                                .replace_config_map(namespace_copy.as_str(), &name, &new_config_map)
                                .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
                                .map(|_| ());

                            Either::A(Either::B(fut))
                        }
                    } else {
                        let fut = client_copy
                            .lock()
                            .expect("Unexpected lock error")
                            .borrow_mut()
                            .create_config_map(namespace_copy.as_str(), &new_config_map)
                            .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
                            .map(|_| ());

                        Either::B(fut)
                    }
                })
                .map_err(|err| Error::from(err.context(ErrorKind::Initialization)))
        })
        .map_err(|err| Error::from(err.context(ErrorKind::Initialization)))
        .into_future()
        .flatten()
}

#[cfg(test)]
mod tests {
    use crate::Error;

    use failure::Fail;
    use hyper::service::service_fn;
    use hyper::{Body, Error as HyperError, Method, Request, Response, StatusCode};
    use maplit::btreemap;
    use serde_json::json;
    use tokio::runtime::Runtime;

    use edgelet_test_utils::cert::TestCert;
    use edgelet_test_utils::crypto::TestHsm;
    use edgelet_test_utils::routes;
    use edgelet_test_utils::web::{
        make_req_dispatcher, HttpMethod, RequestHandler, RequestPath, ResponseFuture,
    };

    use crate::module::init_trust_bundle;
    use crate::tests::{
        create_runtime, make_settings, not_found_handler, response,
        PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME,
    };
    use crate::ErrorKind;

    #[test]
    fn it_fails_when_trust_bundle_unavailable() {
        let settings = make_settings(None);

        let service = service_fn(|_: Request<Body>| -> Result<Response<Body>, HyperError> {
            Ok(Response::new(Body::empty()))
        });
        let crypto = TestHsm::default().with_fail_call(true);

        let runtime = create_runtime(settings, service);
        let task = init_trust_bundle(&runtime, crypto);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();

        assert_eq!(err.kind(), &ErrorKind::Initialization);

        let cause = Fail::iter_causes(&err)
            .next()
            .and_then(|cause| cause.downcast_ref::<Error>())
            .map(Error::kind);
        assert_eq!(cause, Some(&ErrorKind::IdentityCertificate))
    }

    #[test]
    fn it_fails_when_cert_unavailable() {
        let settings = make_settings(None);

        let service = service_fn(|_: Request<Body>| -> Result<Response<Body>, HyperError> {
            Ok(Response::new(Body::empty()))
        });
        let cert = TestCert::default().with_fail_pem(true);
        let crypto = TestHsm::default().with_cert(cert);

        let runtime = create_runtime(settings, service);
        let task = init_trust_bundle(&runtime, crypto);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();

        assert_eq!(err.kind(), &ErrorKind::Initialization);

        let cause = Fail::iter_causes(&err)
            .next()
            .and_then(|cause| cause.downcast_ref::<Error>())
            .map(Error::kind);
        assert_eq!(cause, Some(&ErrorKind::IdentityCertificate))
    }

    #[test]
    fn it_fails_when_k8s_api_call_fails() {
        let settings = make_settings(None);

        let service = service_fn(|_: Request<Body>| -> Result<Response<Body>, HyperError> {
            Ok(Response::new(Body::empty()))
        });
        let cert = TestCert::default().with_cert(b"secret_cert".to_vec());
        let crypto = TestHsm::default().with_cert(cert);

        let runtime = create_runtime(settings, service);
        let task = init_trust_bundle(&runtime, crypto);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();

        assert_eq!(err.kind(), &ErrorKind::Initialization);

        let cause = Fail::iter_causes(&err)
            .next()
            .and_then(|cause| cause.downcast_ref::<Error>())
            .map(Error::kind);
        assert_eq!(cause, Some(&ErrorKind::KubeClient))
    }

    #[test]
    fn it_updates_existing_trust_bundle_config_map() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/configmaps", settings.namespace()) => config_map_list(),
            PUT format!("/api/v1/namespaces/{}/configmaps/{}", settings.namespace(), settings.proxy().trust_bundle_config_map_name()) => update_config_map(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let cert = TestCert::default().with_cert(b"secret_cert".to_vec());
        let crypto = TestHsm::default().with_cert(cert);

        let task = init_trust_bundle(&runtime, crypto);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_creates_new_trust_bundle_config_map() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/configmaps", settings.namespace()) => empty_config_map_list(),
            POST format!("/api/v1/namespaces/{}/configmaps", settings.namespace()) => create_config_map(),
        );
        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let cert = TestCert::default().with_cert(b"secret_cert".to_vec());
        let crypto = TestHsm::default().with_cert(cert);

        let task = init_trust_bundle(&runtime, crypto);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    fn config_map_list() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "ConfigMapList",
                    "apiVersion": "v1",
                    "items": [
                        {
                            "metadata": {
                                "name": PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME,
                                "namespace": "default",
                            },
                            "data": {
                                "cert.pem": "trust bundle"
                            }
                        }
                    ]
                })
                .to_string()
            })
        }
    }

    fn update_config_map() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "ConfigMap",
                    "apiVersion": "v1",
                    "metadata": {
                        "name": PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME,
                        "namespace": "default",
                    }
                })
                .to_string()
            })
        }
    }

    fn create_config_map() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::CREATED, || {
                json!({
                    "kind": "ConfigMap",
                    "apiVersion": "v1",
                    "metadata": {
                        "name": "c",
                        "namespace": "default",
                    }
                })
                .to_string()
            })
        }
    }

    fn empty_config_map_list() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "ConfigMapList",
                    "apiVersion": "v1",
                    "items": []
                })
                .to_string()
            })
        }
    }
}
