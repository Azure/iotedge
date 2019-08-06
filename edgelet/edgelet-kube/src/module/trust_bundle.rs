// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;
use futures::future::Either;
use futures::{future, Future, IntoFuture, Stream};
use hyper::service::Service;
use hyper::Body;

use edgelet_core::GetTrustBundle;
use kube_client::{Error as KubeClientError, TokenSource};

use crate::convert::trust_bundle_to_config_map;
use crate::{Error, ErrorKind, KubeModuleRuntime};

pub fn init_trust_bundle<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    crypto: &impl GetTrustBundle,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource,
    S: Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Into<KubeClientError>,
{
    crypto
        .get_trust_bundle()
        .map_err(|err| Error::from(err.context(ErrorKind::IdentityCertificate)))
        .and_then(|cert| trust_bundle_to_config_map(runtime.settings(), &cert).map_err(Error::from))
        .map(|(name, new_config_map)| {
            let client_copy = runtime.client().clone();
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
                .map_err(Error::from)
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
                                .map_err(Error::from)
                                .map(|_| ());

                            Either::A(Either::B(fut))
                        }
                    } else {
                        let fut = client_copy
                            .lock()
                            .expect("Unexpected lock error")
                            .borrow_mut()
                            .create_config_map(namespace_copy.as_str(), &new_config_map)
                            .map_err(Error::from)
                            .map(|_| ());

                        Either::B(fut)
                    }
                })
        })
        .into_future()
        .flatten()
}

#[cfg(test)]
mod tests {
    use futures::future;
    use hyper::service::{service_fn, Service};
    use hyper::{Body, Error as HyperError, Method, Request, Response, StatusCode};
    use maplit::btreemap;
    use native_tls::TlsConnector;
    use serde_json::json;
    use tokio::runtime::Runtime;
    use typed_headers::{mime, ContentLength, ContentType, HeaderMapExt};
    use url::Url;

    use edgelet_test_utils::cert::TestCert;
    use edgelet_test_utils::crypto::TestHsm;
    use edgelet_test_utils::routes;
    use edgelet_test_utils::web::{
        make_req_dispatcher, HttpMethod, RequestHandler, RequestPath, ResponseFuture,
    };
    use kube_client::{Client as KubeClient, Config as KubeConfig, Error, TokenSource};

    use crate::module::init_trust_bundle;
    use crate::tests::make_settings;
    use crate::{constants, Settings};
    use crate::{ErrorKind, KubeModuleRuntime};

    #[test]
    fn it_fails_when_trust_bundle_unavailable() {
        let settings = make_settings(None);

        let service = service_fn(|_: Request<Body>| -> Result<Response<Body>, HyperError> {
            Ok(Response::new(Body::empty()))
        });
        let crypto = TestHsm::default().with_fail_call(true);

        let runtime = create_runtime(settings, service);
        let task = init_trust_bundle(&runtime, &crypto);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();

        assert_eq!(err.kind(), &ErrorKind::IdentityCertificate)
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
        let task = init_trust_bundle(&runtime, &crypto);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();

        assert_eq!(err.kind(), &ErrorKind::IdentityCertificate)
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
        let task = init_trust_bundle(&runtime, &crypto);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();

        assert_eq!(err.kind(), &ErrorKind::KubeClient)
    }

    #[test]
    fn it_updates_existing_trust_bundle_config_map() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/configmaps", settings.namespace()) => config_map_list(),
            PUT format!("/api/v1/namespaces/{}/configmaps/{}", settings.namespace(), constants::PROXY_CONFIG_TRUST_BUNDLE_NAME) => update_config_map(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let cert = TestCert::default().with_cert(b"secret_cert".to_vec());
        let crypto = TestHsm::default().with_cert(cert);

        let task = init_trust_bundle(&runtime, &crypto);

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

        let task = init_trust_bundle(&runtime, &crypto);

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
                                "name": constants::PROXY_CONFIG_TRUST_BUNDLE_NAME,
                                "namespace": "my-namespace",
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
                        "name": constants::PROXY_CONFIG_TRUST_BUNDLE_NAME,
                        "namespace": "my-namespace",
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
                        "namespace": "my-namespace",
                    }
                })
                .to_string()
            })
        }
    }

    fn empty_config_map_list() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |req| {
            println!("{:?}", req);
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

    fn response(
        status_code: StatusCode,
        response: impl Fn() -> String + Clone + Send + 'static,
    ) -> ResponseFuture {
        let response = response();
        let response_len = response.len();

        let mut response = Response::new(response.into());
        *response.status_mut() = status_code;
        response
            .headers_mut()
            .typed_insert(&ContentLength(response_len as u64));
        response
            .headers_mut()
            .typed_insert(&ContentType(mime::APPLICATION_JSON));

        Box::new(future::ok(response)) as ResponseFuture
    }

    fn not_found_handler(_: Request<Body>) -> ResponseFuture {
        let response = Response::builder()
            .status(StatusCode::NOT_FOUND)
            .body(Body::default())
            .unwrap();

        Box::new(future::ok(response))
    }

    fn create_runtime<S: Service>(
        settings: Settings,
        service: S,
    ) -> KubeModuleRuntime<TestTokenSource, S> {
        let client = KubeClient::with_client(get_config(), service);

        KubeModuleRuntime::new(client, settings)
    }

    fn get_config() -> KubeConfig<TestTokenSource> {
        KubeConfig::new(
            Url::parse("https://localhost:443").unwrap(),
            "/api".to_string(),
            TestTokenSource,
            TlsConnector::new().unwrap(),
        )
    }

    #[derive(Clone)]
    struct TestTokenSource;

    impl TokenSource for TestTokenSource {
        type Error = Error;

        fn get(&self) -> kube_client::error::Result<Option<String>> {
            Ok(None)
        }
    }
}
