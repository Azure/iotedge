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
use k8s_openapi::api::apps::v1 as api_apps;
use k8s_openapi::api::authentication::v1 as api_auth;
use k8s_openapi::api::core::v1 as api_core;
use k8s_openapi::api::rbac::v1 as api_rbac;
use k8s_openapi::apimachinery::pkg::apis::meta::v1 as api_meta;
use k8s_openapi::{http, Response as K8sResponse, ResponseBody};
use log::debug;

use crate::config::{Config, TokenSource};
use crate::error::{Error, ErrorKind};

pub struct HttpClient<C, B>(pub HyperClient<C, B>);

impl<C, B> Service for HttpClient<C, B>
where
    C: Connect + Sync + 'static,
    B: Payload + Send,
{
    type ReqBody = B;
    type ResBody = Body;
    type Error = HyperError;
    type Future = ResponseFuture;

    fn call(&mut self, req: Request<B>) -> Self::Future {
        self.0.request(req)
    }
}

#[derive(Clone)]
pub struct Client<T, S> {
    config: Config<T>,
    client: S,
}

impl<T: TokenSource> Client<T, HttpClient<HttpsConnector<HttpConnector>, Body>> {
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

// with_client method lives in its own block because we don't need whole set of constrains
// everywhere in the code, in tests for instance
impl<T: TokenSource, S> Client<T, S> {
    pub fn with_client(config: Config<T>, client: S) -> Self {
        Client { config, client }
    }
}

impl<T: TokenSource, S> Client<T, S>
where
    S: Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<<S as Service>::ResBody>,
    S::Error: Into<Error>,
{
    pub fn list_config_maps(
        &mut self,
        namespace: &str,
        name: Option<&str>,
        label_selector: Option<&str>,
    ) -> impl Future<Item = api_core::ConfigMapList, Error = Error> {
        let field_selector = name.map(|name| format!("metadata.name={}", name));
        let params = api_core::ListNamespacedConfigMapOptional {
            field_selector: field_selector.as_ref().map(String::as_ref),
            label_selector,
            ..api_core::ListNamespacedConfigMapOptional::default()
        };

        api_core::ConfigMap::list_namespaced_config_map(namespace, params)
            .map_err(Error::from)
            .map(|req| {
                self.request(req).and_then(|response| match response {
                    api_core::ListNamespacedConfigMapResponse::Ok(list) => Ok(list),
                    _ => Err(Error::from(ErrorKind::Response)),
                })
            })
            .into_future()
            .flatten()
    }

    pub fn create_config_map(
        &mut self,
        namespace: &str,
        config_map: &api_core::ConfigMap,
    ) -> impl Future<Item = api_core::ConfigMap, Error = Error> {
        api_core::ConfigMap::create_namespaced_config_map(
            namespace,
            &config_map,
            api_core::CreateNamespacedConfigMapOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::CreateNamespacedConfigMapResponse::Accepted(config_map)
                | api_core::CreateNamespacedConfigMapResponse::Created(config_map)
                | api_core::CreateNamespacedConfigMapResponse::Ok(config_map) => Ok(config_map),
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn replace_config_map(
        &mut self,
        namespace: &str,
        name: &str,
        config_map: &api_core::ConfigMap,
    ) -> impl Future<Item = api_core::ConfigMap, Error = Error> {
        api_core::ConfigMap::replace_namespaced_config_map(
            name,
            namespace,
            config_map,
            api_core::ReplaceNamespacedConfigMapOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::ReplaceNamespacedConfigMapResponse::Ok(config_map)
                | api_core::ReplaceNamespacedConfigMapResponse::Created(config_map) => {
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
        api_core::ConfigMap::delete_namespaced_config_map(
            name,
            namespace,
            api_core::DeleteNamespacedConfigMapOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::DeleteNamespacedConfigMapResponse::OkStatus(_)
                | api_core::DeleteNamespacedConfigMapResponse::OkValue(_) => Ok(()),
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn replace_deployment(
        &mut self,
        namespace: &str,
        name: &str,
        deployment: &api_apps::Deployment,
    ) -> impl Future<Item = api_apps::Deployment, Error = Error> {
        api_apps::Deployment::replace_namespaced_deployment(
            name,
            namespace,
            deployment,
            api_apps::ReplaceNamespacedDeploymentOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_apps::ReplaceNamespacedDeploymentResponse::Created(deployment)
                | api_apps::ReplaceNamespacedDeploymentResponse::Ok(deployment) => Ok(deployment),
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
        let params = api_core::ListNamespacedPodOptional {
            label_selector,
            ..api_core::ListNamespacedPodOptional::default()
        };
        api_core::Pod::list_namespaced_pod(namespace, params)
            .map_err(Error::from)
            .map(|req| {
                self.request(req).and_then(|response| match response {
                    api_core::ListNamespacedPodResponse::Ok(list) => Ok(list),
                    _ => Err(Error::from(ErrorKind::Response)),
                })
            })
            .into_future()
            .flatten()
    }

    pub fn list_secrets(
        &mut self,
        namespace: &str,
        name: Option<&str>,
    ) -> impl Future<Item = api_core::SecretList, Error = Error> {
        let field_selector = name.map(|name| format!("metadata.name={}", name));
        let params = api_core::ListNamespacedSecretOptional {
            field_selector: field_selector.as_ref().map(String::as_ref),
            ..api_core::ListNamespacedSecretOptional::default()
        };
        api_core::Secret::list_namespaced_secret(namespace, params)
            .map_err(Error::from)
            .map(|req| {
                self.request(req).and_then(|response| match response {
                    api_core::ListNamespacedSecretResponse::Ok(list) => Ok(list),
                    _ => Err(Error::from(ErrorKind::Response)),
                })
            })
            .into_future()
            .flatten()
    }

    pub fn create_secret(
        &mut self,
        namespace: &str,
        secret: &api_core::Secret,
    ) -> impl Future<Item = api_core::Secret, Error = Error> {
        api_core::Secret::create_namespaced_secret(
            namespace,
            secret,
            api_core::CreateNamespacedSecretOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::CreateNamespacedSecretResponse::Accepted(s)
                | api_core::CreateNamespacedSecretResponse::Created(s)
                | api_core::CreateNamespacedSecretResponse::Ok(s) => Ok(s),
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn replace_secret(
        &mut self,
        namespace: &str,
        name: &str,
        secret: &api_core::Secret,
    ) -> impl Future<Item = api_core::Secret, Error = Error> {
        api_core::Secret::replace_namespaced_secret(
            name,
            namespace,
            secret,
            api_core::ReplaceNamespacedSecretOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::ReplaceNamespacedSecretResponse::Created(s)
                | api_core::ReplaceNamespacedSecretResponse::Ok(s) => Ok(s),
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn token_review(
        &mut self,
        namespace: &str,
        token: &str,
    ) -> impl Future<Item = api_auth::TokenReview, Error = Error> {
        let token = api_auth::TokenReview {
            metadata: Some(api_meta::ObjectMeta {
                namespace: Some(namespace.to_string()),
                ..api_meta::ObjectMeta::default()
            }),
            spec: api_auth::TokenReviewSpec {
                token: Some(token.to_string()),
            },
            ..api_auth::TokenReview::default()
        };

        api_auth::TokenReview::create_token_review(
            &token,
            api_auth::CreateTokenReviewOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_auth::CreateTokenReviewResponse::Created(t)
                | api_auth::CreateTokenReviewResponse::Ok(t) => Ok(t),
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn list_service_accounts(
        &mut self,
        namespace: &str,
        name: Option<&str>,
        label_selector: Option<&str>,
    ) -> impl Future<Item = api_core::ServiceAccountList, Error = Error> {
        let field_selector = name.map(|name| format!("metadata.name={}", name));
        let params = api_core::ListNamespacedServiceAccountOptional {
            field_selector: field_selector.as_ref().map(String::as_ref),
            label_selector,
            ..api_core::ListNamespacedServiceAccountOptional::default()
        };

        api_core::ServiceAccount::list_namespaced_service_account(namespace, params)
            .map_err(Error::from)
            .map(|req| {
                self.request(req).and_then(|response| match response {
                    api_core::ListNamespacedServiceAccountResponse::Ok(list) => Ok(list),
                    _ => Err(Error::from(ErrorKind::Response)),
                })
            })
            .into_future()
            .flatten()
    }

    pub fn create_service_account(
        &mut self,
        namespace: &str,
        service_account: &api_core::ServiceAccount,
    ) -> impl Future<Item = api_core::ServiceAccount, Error = Error> {
        api_core::ServiceAccount::create_namespaced_service_account(
            namespace,
            &service_account,
            api_core::CreateNamespacedServiceAccountOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::CreateNamespacedServiceAccountResponse::Accepted(service_account)
                | api_core::CreateNamespacedServiceAccountResponse::Created(service_account)
                | api_core::CreateNamespacedServiceAccountResponse::Ok(service_account) => {
                    Ok(service_account)
                }
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn get_service_account(
        &mut self,
        namespace: &str,
        name: &str,
    ) -> impl Future<Item = api_core::ServiceAccount, Error = Error> {
        api_core::ServiceAccount::read_namespaced_service_account(
            name,
            namespace,
            api_core::ReadNamespacedServiceAccountOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::ReadNamespacedServiceAccountResponse::Ok(service_account) => {
                    Ok(service_account)
                }
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn replace_service_account(
        &mut self,
        namespace: &str,
        name: &str,
        service_account: &api_core::ServiceAccount,
    ) -> impl Future<Item = api_core::ServiceAccount, Error = Error> {
        api_core::ServiceAccount::replace_namespaced_service_account(
            name,
            namespace,
            service_account,
            api_core::ReplaceNamespacedServiceAccountOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_core::ReplaceNamespacedServiceAccountResponse::Created(service_account)
                | api_core::ReplaceNamespacedServiceAccountResponse::Ok(service_account) => {
                    Ok(service_account)
                }
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    pub fn replace_role_binding(
        &mut self,
        namespace: &str,
        name: &str,
        role_binding: &api_rbac::RoleBinding,
    ) -> impl Future<Item = api_rbac::RoleBinding, Error = Error> {
        api_rbac::RoleBinding::replace_namespaced_role_binding(
            name,
            namespace,
            role_binding,
            api_rbac::ReplaceNamespacedRoleBindingOptional::default(),
        )
        .map_err(Error::from)
        .map(|req| {
            self.request(req).and_then(|response| match response {
                api_rbac::ReplaceNamespacedRoleBindingResponse::Created(role_binding)
                | api_rbac::ReplaceNamespacedRoleBindingResponse::Ok(role_binding) => {
                    Ok(role_binding)
                }
                _ => Err(Error::from(ErrorKind::Response)),
            })
        })
        .into_future()
        .flatten()
    }

    #[allow(clippy::type_complexity)]
    fn request<R: K8sResponse>(
        &mut self,
        (req, _response_body): (
            http::Request<Vec<u8>>,
            fn(http::StatusCode) -> ResponseBody<R>,
        ),
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
    use k8s_openapi::api::apps::v1 as api_apps;
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

    const DEPLOYMENT_JSON: &str = r##"{"apiVersion":"apps/v1","kind":"Deployment"}"##;

    #[test]
    fn replace_deployment_success() {
        const NAMESPACE: &str = "custom-namespace";
        const NAME: &str = "deployment-name";
        let service1 = service_fn(
            move |req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let p = req.uri().path();
                assert!(p.contains(NAMESPACE));
                assert!(p.contains(NAME));
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

        let deployment: api_apps::Deployment = serde_json::from_str(DEPLOYMENT_JSON).unwrap();
        let fut = client.replace_deployment(NAME, NAMESPACE, &deployment);

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
            assert_eq!(2, pods.items.len());
        });

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn list_pods_error_response() {
        const NAMESPACE: &str = "custom-namespace";
        const LABEL_SELECTOR: &str = "x=y";
        let service = service_fn(
            |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let mut res = Response::new(Body::empty());
                *res.status_mut() = StatusCode::SERVICE_UNAVAILABLE;
                Ok(res)
            },
        );

        let mut client = make_test_client(service);

        let fut = client.list_pods(NAMESPACE, Some(LABEL_SELECTOR));
        let _ = Runtime::new()
            .unwrap()
            .block_on(fut)
            .map_err(|e| {
                assert!(e.to_string().contains("HTTP response error"));
            })
            .map(|r| {
                panic!("expected an error result {:?}", r);
            });
    }

    #[test]
    fn list_pods_service_error() {
        const NAMESPACE: &str = "custom-namespace";
        const LABEL_SELECTOR: &str = "x=y";
        let service = service_fn(
            |_req: Request<Body>| -> std::result::Result<Response<Body>, _> {
                Err("Some terrible error")
            },
        );

        let mut client = make_test_client(service);

        let fut = client.list_pods(NAMESPACE, Some(LABEL_SELECTOR));
        let _ = Runtime::new()
            .unwrap()
            .block_on(fut)
            .map_err(|e| {
                assert!(e.to_string().contains("HTTP test error"));
            })
            .map(|r| {
                panic!("expected an error result {:?}", r);
            });
    }
    #[test]
    fn replace_deployment_error_response() {
        const NAMESPACE: &str = "custom-namespace";
        const NAME: &str = "deployment1";
        let service1 = service_fn(
            move |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let mut res = Response::new(Body::from(DEPLOYMENT_JSON));
                *res.status_mut() = StatusCode::CONFLICT;
                Ok(res)
            },
        );

        let mut client = make_test_client(service1);

        let deployment: api_apps::Deployment = serde_json::from_str(DEPLOYMENT_JSON).unwrap();
        let fut = client.replace_deployment(NAME, NAMESPACE, &deployment);
        if let Ok(r) = Runtime::new().unwrap().block_on(fut) {
            panic!("expected an error result {:?}", r);
        }
    }

    const LIST_SECRET_RESPONSE: &str = r###"{
            "kind" : "SecretList",
            "items" : [
                {
                    "kind" : "Secret"
                }
            ]
            }"###;
    #[test]
    fn list_secrets_with_name_success() {
        const NAMESPACE: &str = "custom-namespace";
        const NAME: &str = "secret1";
        const FIELD_SELECTOR: &str = "metadata.name=secret1";
        let service = service_fn(|req: Request<Body>| -> Result<Response<Body>, HyperError> {
            let p = req.uri().path();
            let q = req.uri().query().unwrap();
            assert!(p.contains(NAMESPACE));
            assert!(
                q.contains(&utf8_percent_encode(FIELD_SELECTOR, USERINFO_ENCODE_SET).to_string())
            );
            Ok(Response::new(Body::from(LIST_SECRET_RESPONSE)))
        });
        let mut client = make_test_client(service);

        let fut = client.list_secrets(NAMESPACE, Some(NAME)).map(|secrets| {
            assert_eq!(1, secrets.items.len());
        });

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    const SECRET_JSON: &str = r##"{"apiVersion":"v1","kind":"Secret"}"##;
    #[test]
    fn create_secret_success() {
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
                        assert_eq!(::std::str::from_utf8(&buf).unwrap(), SECRET_JSON);
                        future::ok(())
                    })
                    .wait()
                    .expect("Unexpected result");
                let mut res = Response::new(Body::from(SECRET_JSON));
                *res.status_mut() = StatusCode::CREATED;
                Ok(res)
            },
        );

        let mut client = make_test_client(service1);
        let result = serde_json::from_str(SECRET_JSON);
        let result = result.map_err(|e| println!("{:?}", e));

        let secret: api_core::Secret = result.unwrap();
        let fut = client.create_secret(NAMESPACE, &secret);

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn replace_secret_success() {
        const NAMESPACE: &str = "custom-namespace";
        const NAME: &str = "secret1";
        let service1 = service_fn(
            move |req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let p = req.uri().path();
                assert!(p.contains(NAMESPACE));
                assert!(p.contains(NAME));
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
                        assert_eq!(::std::str::from_utf8(&buf).unwrap(), SECRET_JSON);
                        future::ok(())
                    })
                    .wait()
                    .expect("Unexpected result");
                let mut res = Response::new(Body::from(SECRET_JSON));
                *res.status_mut() = StatusCode::CREATED;
                Ok(res)
            },
        );

        let mut client = make_test_client(service1);

        let secret: api_core::Secret = serde_json::from_str(SECRET_JSON).unwrap();
        let fut = client.replace_secret(NAME, NAMESPACE, &secret);

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn create_service_error_response() {
        let service_fn = service_fn(
            move |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let mut res = Response::new(Body::from(SECRET_JSON));
                *res.status_mut() = StatusCode::CONFLICT;
                Ok(res)
            },
        );

        let mut client = make_test_client(service_fn);
        let secret: api_core::Secret = serde_json::from_str(SECRET_JSON).unwrap();
        let fut = client.create_secret("NAMESPACE", &secret);

        if let Ok(r) = Runtime::new().unwrap().block_on(fut) {
            panic!("expected an error result {:?}", r);
        }
    }

    #[test]
    fn replace_service_error_response() {
        let service_fn = service_fn(
            move |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let mut res = Response::new(Body::from(SECRET_JSON));
                *res.status_mut() = StatusCode::CONFLICT;
                Ok(res)
            },
        );

        let mut client = make_test_client(service_fn);
        let secret: api_core::Secret = serde_json::from_str(SECRET_JSON).unwrap();
        let fut = client.replace_secret("NAME", "NAMESPACE", &secret);

        if let Ok(r) = Runtime::new().unwrap().block_on(fut) {
            panic!("expected an error result {:?}", r);
        }
    }

    const TOKEN_REVIEW_JSON: &str = r###"{"apiVersion":"authentication.k8s.io/v1","kind":"TokenReview","metadata":{"namespace":"NAMESPACE"},"spec":{"token":"BEARERTOKEN"}}"###;

    const TOKEN_REVIEW_AUTHENTICATED_RESPONSE_JSON: &str = r###"{
        "kind": "TokenReview",
        "spec": { "token": "BEARERTOKEN" },
        "status": {
            "authenticated": true,
            "user": {
                "username": "module-abc"
            }
        }
        }"###;

    #[test]
    fn token_review_success() {
        let service_fn = service_fn(|req: Request<Body>| -> Result<Response<Body>, HyperError> {
            req.into_body()
                .map_err(|_| ())
                .fold(BytesMut::new(), |mut buf, chunk| {
                    buf.extend_from_slice(chunk.as_ref());
                    future::ok::<_, ()>(buf)
                })
                .map_err(|_| ())
                .and_then(move |buf| {
                    assert_eq!(::std::str::from_utf8(&buf).unwrap(), TOKEN_REVIEW_JSON);
                    future::ok(())
                })
                .wait()
                .expect("Unexpected result");

            let mut res = Response::new(Body::from(TOKEN_REVIEW_AUTHENTICATED_RESPONSE_JSON));
            *res.status_mut() = StatusCode::CREATED;
            Ok(res)
        });

        let mut client = make_test_client(service_fn);
        let token = "BEARERTOKEN";
        let fut = client.token_review("NAMESPACE", token);

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .map(|r| {
                let status = r.status.unwrap();
                assert_eq!(Some(true), status.authenticated);
                assert_eq!(
                    Some("module-abc".to_string()),
                    status.user.unwrap().username
                );
            })
            .expect("Expected future to be OK");
    }

    #[test]
    fn token_review_error_response() {
        let service_fn = service_fn(
            |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let mut res = Response::new(Body::empty());
                *res.status_mut() = StatusCode::SERVICE_UNAVAILABLE;
                Ok(res)
            },
        );

        let mut client = make_test_client(service_fn);
        let token = "BEARERTOKEN";
        let fut = client.token_review("NAMESPACE", token);

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
