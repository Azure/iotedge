// Copyright (c) Microsoft. All rights reserved.

use bytes::BytesMut;
use futures::future::{self, Either};
use futures::prelude::*;
use hyper::client::{Client as HyperClient, HttpConnector};
use hyper::{Body, Error as HyperError};
use hyper_tls::HttpsConnector;
use k8s_openapi::v1_10::api::apps::v1 as apps;
use k8s_openapi::v1_10::api::core::v1 as api_core;
use k8s_openapi::{http, Response as K8sResponse};
use log::debug;

use config::{Config, TokenSource};
use error::{Error, ErrorKind};

#[derive(Clone)]
pub struct Client<T: Clone> {
    config: Config<T>,
    client: HyperClient<HttpsConnector<HttpConnector>>,
}

impl<T: TokenSource + Clone> Client<T> {
    pub fn new(config: Config<T>) -> Client<T> {
        let mut http = HttpConnector::new(4);
        // if we don't do this then the HttpConnector rejects the "https" scheme
        http.enforce_http(false);

        let connector = (http, config.tls_connector().clone()).into();
        Client {
            config,
            client: HyperClient::builder().build::<_, Body>(connector),
        }
    }

    pub fn create_config_map(
        &self,
        namespace: &str,
        config_map: &api_core::ConfigMap,
    ) -> impl Future<Item = api_core::ConfigMap, Error = Error> {
        api_core::ConfigMap::create_core_v1_namespaced_config_map(namespace, config_map, None)
            .map_err(Error::from)
            .map(|req| {
                let fut = self.request(req).and_then(|response| match response {
                    api_core::CreateCoreV1NamespacedConfigMapResponse::Ok(config_map)
                    | api_core::CreateCoreV1NamespacedConfigMapResponse::Created(config_map)
                    | api_core::CreateCoreV1NamespacedConfigMapResponse::Accepted(config_map) => {
                        Ok(config_map)
                    }
                    err => {
                        debug!("Create config map failed with {:#?}", err);
                        Err(Error::from(ErrorKind::Response))
                    }
                });

                Either::A(fut)
            })
            .unwrap_or_else(|err| Either::B(future::err(err)))
    }

    pub fn delete_config_map(
        &self,
        namespace: &str,
        name: &str,
    ) -> impl Future<Item = (), Error = Error> {
        api_core::ConfigMap::delete_core_v1_namespaced_config_map(
            name, namespace, None, None, None, None,
        )
        .map_err(Error::from)
        .map(|req| {
            let fut = self.request(req).and_then(|response| match response {
                api_core::DeleteCoreV1NamespacedConfigMapResponse::OkStatus(_)
                | api_core::DeleteCoreV1NamespacedConfigMapResponse::OkValue(_) => Ok(()),
                _ => Err(Error::from(ErrorKind::Response)),
            });

            Either::A(fut)
        })
        .unwrap_or_else(|err| Either::B(future::err(err)))
    }

    pub fn create_deployment(
        &self,
        namespace: &str,
        deployment: &apps::Deployment,
    ) -> impl Future<Item = apps::Deployment, Error = Error> {
        apps::Deployment::create_apps_v1_namespaced_deployment(namespace, &deployment, None)
            .map_err(Error::from)
            .map(|req| {
                let fut = self.request(req).and_then(|response| match response {
                    apps::CreateAppsV1NamespacedDeploymentResponse::Accepted(deployment)
                    | apps::CreateAppsV1NamespacedDeploymentResponse::Created(deployment)
                    | apps::CreateAppsV1NamespacedDeploymentResponse::Ok(deployment) => {
                        Ok(deployment)
                    }
                    _ => Err(Error::from(ErrorKind::Response)),
                });

                Either::A(fut)
            })
            .unwrap_or_else(|err| Either::B(future::err(err)))
    }

    pub fn list_pods(
        &self,
        namespace: &str,
    ) -> impl Future<Item = api_core::PodList, Error = Error> {
        api_core::Pod::list_core_v1_namespaced_pod(
            namespace, None, None, None, None, None, None, None, None, None,
        )
        .map_err(Error::from)
        .map(|req| {
            let fut = self.request(req).and_then(|response| match response {
                api_core::ListCoreV1NamespacedPodResponse::Ok(pod_list) => Ok(pod_list),
                _ => Err(Error::from(ErrorKind::Response)),
            });

            Either::A(fut)
        })
        .unwrap_or_else(|err| Either::B(future::err(err)))
    }

    fn request<R: K8sResponse>(
        &self,
        req: http::Request<Vec<u8>>,
    ) -> impl Future<Item = R, Error = Error> {
        let next = |response: http::Response<Body>| {
            let status_code = response.status();
            response
                .into_body()
                .fold(BytesMut::new(), |mut buf, chunk| {
                    buf.extend_from_slice(&chunk);
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
        &self,
        mut req: http::Request<Vec<u8>>,
    ) -> impl Future<Item = http::Response<Body>, Error = Error> {
        self.config
            .host()
            .join(self.config.api_path())
            .and_then(|base_url| {
                base_url.join(
                    req.uri()
                        .path_and_query()
                        .map(|pq| pq.as_str())
                        .unwrap_or(""),
                )
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
                // Request<Body>.
                Either::A(
                    self.client
                        .request(req.map(From::from))
                        .map_err(Error::from),
                )
            })
            .unwrap_or_else(|err| Either::B(future::err(err)))
    }
}
