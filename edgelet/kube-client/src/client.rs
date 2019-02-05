// Copyright (c) Microsoft. All rights reserved.

use bytes::BytesMut;
use futures::future;
use futures::prelude::*;
use hyper::body::Payload;
use hyper::client::connect::Connect;
use hyper::client::{Client as HyperClient, HttpConnector, ResponseFuture};
use hyper::service::Service;
use hyper::Request;
use hyper::{Body, Error as HyperError};
use hyper_tls::HttpsConnector;
use k8s_openapi::v1_10::api::apps::v1 as apps;
use k8s_openapi::v1_10::api::core::v1 as api_core;
use k8s_openapi::v1_10::apimachinery::pkg::apis::meta::v1 as api_meta;
use k8s_openapi::{http, Response as K8sResponse};
use log::debug;

use crate::config::{Config, TokenSource};
use crate::error::{Error, ErrorKind};

pub struct HttpClient<C, B>(HyperClient<C, B>);

impl<C, B> Service for HttpClient<C, B>
where
    C: Connect + Sync + 'static,
    B: Payload + Send,
{
    type ReqBody = B;
    type ResBody = Body;
    type Future = ResponseFuture;
    type Error = HyperError;

    fn call(&mut self, req: Request<B>) -> Self::Future {
        self.0.request(req)
    }
}

#[derive(Clone)]
pub struct Client<T: Clone, S> {
    config: Config<T>,
    client: S,
}

impl<T: TokenSource + Clone> Client<T, HttpClient<HttpsConnector<HttpConnector>, Body>> {
    pub fn new(config: Config<T>) -> Client<T, HttpClient<HttpsConnector<HttpConnector>, Body>> {
        let mut http = HttpConnector::new(4);
        // if we don't do this then the HttpConnector rejects the "https" scheme
        http.enforce_http(false);

        let connector: HttpsConnector<HttpConnector> =
            (http, config.tls_connector().clone()).into();
        Client {
            config,
            client: HttpClient(HyperClient::builder().build::<_, Body>(connector)),
        }
    }
}

impl<T: TokenSource + Clone, S> Client<T, S>
where
    S: Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<<S as Service>::ResBody>,
    <S::ResBody as Stream>::Item: AsRef<[u8]>,
    <S::ResBody as Stream>::Error: Into<Error>,
    S::Error: Into<Error>,
{
    pub fn create_config_map(
        &mut self,
        namespace: &str,
        config_map: &api_core::ConfigMap,
    ) -> impl Future<Item = api_core::ConfigMap, Error = Error> {
        api_core::ConfigMap::create_core_v1_namespaced_config_map(namespace, config_map, None)
            .map_err(Error::from)
            .map(|req| {
                self.request(req).and_then(|response| match response {
                    api_core::CreateCoreV1NamespacedConfigMapResponse::Ok(config_map)
                    | api_core::CreateCoreV1NamespacedConfigMapResponse::Created(config_map)
                    | api_core::CreateCoreV1NamespacedConfigMapResponse::Accepted(config_map) => {
                        Ok(config_map)
                    }
                    err => {
                        debug!("Create config map failed with {:#?}", err);
                        Err(Error::from(ErrorKind::Response))
                    }
                })
            })
            .into_future()
            .flatten()
    }

    pub fn delete_config_map(
        &mut self,
        namespace: &str,
        name: &str,
    ) -> impl Future<Item = (), Error = Error> {
        api_core::ConfigMap::delete_core_v1_namespaced_config_map(
            name, namespace, None, None, None, None,
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::DeleteCoreV1NamespacedConfigMapResponse::OkStatus(_)
                | api_core::DeleteCoreV1NamespacedConfigMapResponse::OkValue(_) => Ok(()),
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn create_deployment(
        &mut self,
        namespace: &str,
        deployment: &apps::Deployment,
    ) -> impl Future<Item = apps::Deployment, Error = Error> {
        apps::Deployment::create_apps_v1_namespaced_deployment(namespace, &deployment, None)
            .map_err(Error::from)
            .map(|req| {
                self.request(req).and_then(|response| match response {
                    apps::CreateAppsV1NamespacedDeploymentResponse::Accepted(deployment)
                    | apps::CreateAppsV1NamespacedDeploymentResponse::Created(deployment)
                    | apps::CreateAppsV1NamespacedDeploymentResponse::Ok(deployment) => {
                        Ok(deployment)
                    }
                    _ => Err(Error::from(ErrorKind::Response)),
                })
            })
            .into_future()
            .flatten()
    }

    pub fn delete_deployment(
        &mut self,
        namespace: &str,
        name: &str,
        options: Option<&api_meta::DeleteOptions>,
    ) -> impl Future<Item = (()), Error = Error> {
        apps::Deployment::delete_apps_v1_namespaced_deployment(
            name,
            namespace,
            options.and_then(|o| o.grace_period_seconds),
            None,
            None,
            options.and_then(|o| o.propagation_policy.as_ref().map(|p| p.as_str())),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                apps::DeleteAppsV1NamespacedDeploymentResponse::OkStatus(_)
                | apps::DeleteAppsV1NamespacedDeploymentResponse::OkValue(_) => Ok(()),
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn list_pods(
        &mut self,
        namespace: &str,
        label_selector: Option<&str>,
    ) -> impl Future<Item = api_core::PodList, Error = Error> {
        api_core::Pod::list_core_v1_namespaced_pod(
            namespace,
            None,
            None,
            None,
            label_selector,
            None,
            None,
            None,
            None,
            None,
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::ListCoreV1NamespacedPodResponse::Ok(pod_list) => Ok(pod_list),
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    fn request<R: K8sResponse>(
        &mut self,
        req: http::Request<Vec<u8>>,
    ) -> impl Future<Item = R, Error = Error> {
        let next = |response: http::Response<Body>| {
            let status_code = response.status();
            response
                .into_body()
                .fold(BytesMut::new(), |mut buf, chunk| {
                    buf.extend_from_slice(chunk.as_ref());
                    future::ok::<_, HyperError>(buf)
                })
                .map_err(Error::from)
                .and_then(move |buf| {
                    debug!("HTTP Response:\n{}", ::std::str::from_utf8(&buf).unwrap());
                    R::try_from_parts(status_code, &buf)
                        .map_err(Error::from)
                        .map(|(result, _)| result)
                        .into_future()
                })
        };

        self.execute(req).and_then(next)
    }

    fn execute(
        &mut self,
        mut req: http::Request<Vec<u8>>,
    ) -> impl Future<Item = http::Response<Body>, Error = Error> {
        self.config
            .host()
            .join(self.config.api_path())
            .and_then(|base_url| {
                base_url.join(req.uri().path_and_query().map_or("", |pq| pq.as_str()))
            })
            .map_err(Error::from)
            .and_then(|url| url.as_ref().parse().map_err(Error::from))
            .and_then(|uri| self.config.token_source().get().map(|token| (uri, token)))
            .and_then(|(uri, token)| {
                // set the full URL on the request including API path
                *req.uri_mut() = uri;

                // add the authorization bearer token to the request if we have one
                if let Some(token) = token {
                    let token = format!("Bearer {}", token).parse()?;
                    req.headers_mut().append(http::header::AUTHORIZATION, token);
                }

                Ok(req)
            })
            .map(|req| {
                // NOTE: The req.map call below converts from Request<Vec<u8>> into a
                // Request<Body>. The res.map call converts from S::ResBody to Body.
                self.client
                    .call(req.map(From::from))
                    .map_err(Into::into)
                    .map(|res| res.map(From::from))
            })
            .into_future()
            .flatten()
    }
}

#[cfg(test)]
mod tests {

    use super::*;
    use crate::config::{Config, TokenSource};
    use hyper::service::service_fn;
    use hyper::{Body, Error as HyperError, Request, Response, StatusCode};
    use k8s_openapi::v1_10::api::apps::v1 as apps;
    use k8s_openapi::v1_10::apimachinery::pkg::apis::meta::v1 as api_meta;
    use native_tls::TlsConnector;
    use serde_json;
    use tokio::runtime::Runtime;
    use url::percent_encoding::{utf8_percent_encode, USERINFO_ENCODE_SET};
    use url::Url;

    #[derive(Clone)]
    struct TestTokenSource();

    impl TokenSource for TestTokenSource {
        type Error = super::Error;

        fn get(&self) -> Result<Option<String>, Self::Error> {
            Ok(None)
        }
    }

    const STATUS_SUCCESS: &str = r###"{"kind":"Status", "status":"Success"}"###;
    const DEPLOYMENT_JSON: &str = r##"{"kind":"Deployment"}"##;

    #[test]
    fn create_deployment_success() {
        const NAMESPACE: &str = "custom-namespace";
        let service1 = service_fn(
            move |req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let p = req.uri().path();
                assert!(p.contains(NAMESPACE));
                let q = req.uri().query().unwrap();
                assert!(q.is_empty());
                req.into_body()
                    .map_err(|_| ())
                    .fold(BytesMut::new(), |mut buf, chunk| {
                        buf.extend_from_slice(chunk.as_ref());
                        future::ok::<_, ()>(buf)
                    })
                    .map_err(|_| ())
                    .and_then(move |buf| {
                        assert_eq!(::std::str::from_utf8(&buf).unwrap(), DEPLOYMENT_JSON);
                        future::ok(())
                    })
                    .wait()
                    .expect("Unexpected result");
                let mut res = Response::new(Body::from(DEPLOYMENT_JSON));
                *res.status_mut() = StatusCode::CREATED;
                Ok(res)
            },
        );

        let mut client = make_test_client(service1);

        let deployment: apps::Deployment = serde_json::from_str(DEPLOYMENT_JSON).unwrap();
        let fut = client.create_deployment(NAMESPACE, &deployment);

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn delete_deployment_success() {
        const NAMESPACE: &str = "custom-namespace";
        const NAME: &str = "deployment1";
        let service1 = service_fn(|req: Request<Body>| -> Result<Response<Body>, HyperError> {
            let p = req.uri().path();
            assert!(p.contains(NAMESPACE) && p.contains(NAME));
            let q = req.uri().query().unwrap();
            assert!(q.is_empty());
            Ok(Response::new(Body::from(STATUS_SUCCESS)))
        });

        let mut client = make_test_client(service1);

        let fut = client.delete_deployment(NAMESPACE, NAME, None);

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn delete_deployment_with_options() {
        const NAMESPACE: &str = "custom-namespace";
        const NAME: &str = "deployment1";
        const DELETE_OPTIONS: &str = r###"
    {
        "gracePeriodSeconds": 60,
        "kind": "DeleteOptions",
        "propagationPolicy": "Foreground"
    }
    "###;

        let service2 = service_fn(|req: Request<Body>| -> Result<Response<Body>, HyperError> {
            let p = req.uri().path();
            assert!(p.contains(NAMESPACE) && p.contains(NAME));
            let q = req.uri().query().unwrap();
            assert!(
                q.contains("gracePeriodSeconds=60")
                    && q.contains("propagationPolicy=Foreground")
                    && !q.contains("orphanedDependents")
            );
            Ok(Response::new(Body::from(DEPLOYMENT_JSON)))
        });

        let mut client = make_test_client(service2);

        let options: api_meta::DeleteOptions = serde_json::from_str(DELETE_OPTIONS).unwrap();
        let fut = client.delete_deployment(NAMESPACE, NAME, Some(&options));

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    const LIST_POD_RESPONSE: &str = r###"{
        "kind" : "PodList",
        "items" : [
            {
                "kind" : "Pod"
            },
            {
                "kind" : "Pod"
            }
        ]
    }"###;

    #[test]
    fn list_pods_success() {
        const NAMESPACE: &str = "custom-namespace";
        const LABEL_SELECTOR: &str = "x=y";
        let service = service_fn(|req: Request<Body>| -> Result<Response<Body>, HyperError> {
            let p = req.uri().path();
            let q = req.uri().query().unwrap();
            assert!(p.contains(NAMESPACE));
            assert!(
                q.contains(&utf8_percent_encode(LABEL_SELECTOR, USERINFO_ENCODE_SET).to_string())
            );
            Ok(Response::new(Body::from(LIST_POD_RESPONSE)))
        });

        let mut client = make_test_client(service);

        let fut = client
            .list_pods(NAMESPACE, Some(LABEL_SELECTOR))
            .map(|pods| {
                assert!(pods.kind.as_ref().map_or(false, |k| k == "PodList"));
                assert_eq!(2, pods.items.len());
            });

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn list_pods_success_no_labels() {
        const NAMESPACE: &str = "custom-namespace";
        let service = service_fn(|req: Request<Body>| -> Result<Response<Body>, HyperError> {
            let p = req.uri().path();
            let q = req.uri().query().unwrap();
            assert!(p.contains(NAMESPACE));
            assert!(q.is_empty());
            Ok(Response::new(Body::from(LIST_POD_RESPONSE)))
        });

        let mut client = make_test_client(service);

        let fut = client.list_pods(NAMESPACE, None).map(|pods| {
            assert!(pods.kind.as_ref().map_or(false, |k| k == "PodList"));
            assert_eq!(2, pods.items.len());
        });

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn create_deployment_error_response() {
        const NAMESPACE: &str = "custom-namespace";
        let service1 = service_fn(
            move |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let mut res = Response::new(Body::from(DEPLOYMENT_JSON));
                *res.status_mut() = StatusCode::CONFLICT;
                Ok(res)
            },
        );

        let mut client = make_test_client(service1);

        let deployment: apps::Deployment = serde_json::from_str(DEPLOYMENT_JSON).unwrap();
        let fut = client.create_deployment(NAMESPACE, &deployment);
        if let Ok(r) = Runtime::new().unwrap().block_on(fut) {
            panic!("expected an error result {:?}", r);
        }
    }

    fn make_test_client<S: Service>(service: S) -> Client<TestTokenSource, S> {
        Client {
            config: Config::new(
                Url::parse("http://localhost/").unwrap(),
                "api_path".to_string(),
                TestTokenSource(),
                TlsConnector::new().unwrap(),
            ),
            client: service,
        }
    }

}
