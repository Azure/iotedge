// Copyright (c) Microsoft. All rights reserved.

use bytes::BytesMut;
use failure::{Fail, ResultExt};
use futures::future;
use futures::prelude::*;
use hyper::body::Payload;
use hyper::client::connect::Connect;
use hyper::client::{Client as HyperClient, HttpConnector, ResponseFuture};
use hyper::header::HeaderValue;
use hyper::service::Service;
use hyper::{Body, Error as HyperError};
use hyper::{Request, Uri};
use hyper_tls::HttpsConnector;
use k8s_openapi::api::apps::v1 as api_apps;
use k8s_openapi::api::authentication::v1 as api_auth;
use k8s_openapi::api::authorization::v1 as api_authorize;
use k8s_openapi::api::core::v1 as api_core;
use k8s_openapi::api::rbac::v1 as api_rbac;
use k8s_openapi::apimachinery::pkg::apis::meta::v1 as api_meta;
use k8s_openapi::{
    http, CreateOptional, CreateResponse, DeleteOptional, DeleteResponse, List, ListOptional,
    ListResponse, ReplaceOptional, ReplaceResponse, Response as K8sResponse, ResponseBody,
};
use log::{debug, trace};

use crate::config::{Config, TokenSource};
use crate::error::{Error, ErrorKind, RequestType};

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
    S::Error: Fail,
    Body: From<<S as Service>::ResBody>,
{
    pub fn is_subject_allowed(
        &mut self,
        resource: String,
        verb: String,
    ) -> impl Future<Item = api_authorize::SubjectAccessReviewStatus, Error = Error> {
        let subject_access_review = api_authorize::SelfSubjectAccessReview {
            spec: api_authorize::SelfSubjectAccessReviewSpec {
                resource_attributes: Some(api_authorize::ResourceAttributes {
                    resource: Some(resource),
                    verb: Some(verb),
                    ..api_authorize::ResourceAttributes::default()
                }),
                ..api_authorize::SelfSubjectAccessReviewSpec::default()
            },
            ..api_authorize::SelfSubjectAccessReview::default()
        };

        api_authorize::SelfSubjectAccessReview::create_self_subject_access_review(
            &subject_access_review,
            CreateOptional::default(),
        )
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Request(
                RequestType::SelfSubjectAccessReviewCreate,
            )))
        })
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    CreateResponse::Accepted(s)
                    | CreateResponse::Created(s)
                    | CreateResponse::Ok(s) => Ok(s),
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::SelfSubjectAccessReviewCreate,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(
                        RequestType::SelfSubjectAccessReviewCreate,
                    )))
                })
                .and_then(|ssar| Ok(ssar.status.unwrap_or_default()))
        })
        .into_future()
        .flatten()
    }

    pub fn list_config_maps(
        &mut self,
        namespace: &str,
        name: Option<&str>,
        label_selector: Option<&str>,
    ) -> impl Future<Item = List<api_core::ConfigMap>, Error = Error> {
        let field_selector = name.map(|name| format!("metadata.name={}", name));
        let params = ListOptional {
            field_selector: field_selector.as_ref().map(String::as_ref),
            label_selector,
            ..ListOptional::default()
        };

        api_core::ConfigMap::list_namespaced_config_map(namespace, params)
            .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::ConfigMapList))))
            .map(|req| {
                self.request(req, true)
                    .and_then(|response| match response {
                        ListResponse::Ok(list) => Ok(list),
                        ListResponse::Other(_) => {
                            Err(Error::from(ErrorKind::Response(RequestType::ConfigMapList)))
                        }
                    })
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::Response(RequestType::ConfigMapList)))
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
            CreateOptional::default(),
        )
        .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::ConfigMapCreate))))
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    CreateResponse::Accepted(config_map)
                    | CreateResponse::Created(config_map)
                    | CreateResponse::Ok(config_map) => Ok(config_map),
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::ConfigMapCreate,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::ConfigMapCreate)))
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
            ReplaceOptional::default(),
        )
        .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::ConfigMapReplace))))
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    ReplaceResponse::Ok(config_map) | ReplaceResponse::Created(config_map) => {
                        Ok(config_map)
                    }
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::ConfigMapReplace,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::ConfigMapReplace)))
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
            DeleteOptional::default(),
        )
        .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::ConfigMapDelete))))
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    DeleteResponse::OkStatus(_) | DeleteResponse::OkValue(_) => Ok(()),
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::ConfigMapDelete,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::ConfigMapDelete)))
                })
        })
        .into_future()
        .flatten()
    }

    pub fn list_deployments(
        &mut self,
        namespace: &str,
        name: Option<&str>,
        label_selector: Option<&str>,
    ) -> impl Future<Item = List<api_apps::Deployment>, Error = Error> {
        let field_selector =
            name.map(|deployment_name| format!("metadata.name={}", deployment_name));
        let params = ListOptional {
            field_selector: field_selector.as_ref().map(String::as_ref),
            label_selector,
            ..ListOptional::default()
        };
        api_apps::Deployment::list_namespaced_deployment(namespace, params)
            .map_err(|err| {
                Error::from(err.context(ErrorKind::Request(RequestType::DeploymentList)))
            })
            .map(|req| {
                self.request(req, true)
                    .and_then(|response| match response {
                        ListResponse::Ok(deployments) => Ok(deployments),
                        ListResponse::Other(_) => Err(Error::from(ErrorKind::Response(
                            RequestType::DeploymentList,
                        ))),
                    })
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::Response(RequestType::DeploymentList)))
                    })
            })
            .into_future()
            .flatten()
    }

    pub fn create_deployment(
        &mut self,
        namespace: &str,
        deployment: &api_apps::Deployment,
    ) -> impl Future<Item = api_apps::Deployment, Error = Error> {
        api_apps::Deployment::create_namespaced_deployment(
            namespace,
            &deployment,
            CreateOptional::default(),
        )
        .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::DeploymentCreate))))
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    CreateResponse::Accepted(deployment)
                    | CreateResponse::Created(deployment)
                    | CreateResponse::Ok(deployment) => Ok(deployment),
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::DeploymentCreate,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::DeploymentCreate)))
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
            ReplaceOptional::default(),
        )
        .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::DeploymentReplace))))
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    ReplaceResponse::Created(deployment) | ReplaceResponse::Ok(deployment) => {
                        Ok(deployment)
                    }
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::DeploymentReplace,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::DeploymentReplace)))
                })
        })
        .into_future()
        .flatten()
    }

    pub fn delete_deployment(
        &mut self,
        namespace: &str,
        name: &str,
    ) -> impl Future<Item = (), Error = Error> {
        api_apps::Deployment::delete_namespaced_deployment(
            name,
            namespace,
            DeleteOptional::default(),
        )
        .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::DeploymentDelete))))
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    DeleteResponse::OkStatus(_) | DeleteResponse::OkValue(_) => Ok(()),
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::DeploymentDelete,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::DeploymentDelete)))
                })
        })
        .into_future()
        .flatten()
    }

    pub fn list_pods(
        &mut self,
        namespace: &str,
        label_selector: Option<&str>,
    ) -> impl Future<Item = List<api_core::Pod>, Error = Error> {
        let params = ListOptional {
            label_selector,
            ..ListOptional::default()
        };
        api_core::Pod::list_namespaced_pod(namespace, params)
            .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::PodList))))
            .map(|req| {
                self.request(req, true)
                    .and_then(|response| match response {
                        ListResponse::Ok(list) => Ok(list),
                        ListResponse::Other(_) => {
                            Err(Error::from(ErrorKind::Response(RequestType::PodList)))
                        }
                    })
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::Response(RequestType::PodList)))
                    })
            })
            .into_future()
            .flatten()
    }

    pub fn list_nodes(&mut self) -> impl Future<Item = List<api_core::Node>, Error = Error> {
        api_core::Node::list_node(ListOptional::default())
            .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::NodeList))))
            .map(|req| {
                self.request(req, true)
                    .and_then(|response| match response {
                        ListResponse::Ok(list) => Ok(list),
                        ListResponse::Other(_) => {
                            Err(Error::from(ErrorKind::Response(RequestType::NodeList)))
                        }
                    })
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::Response(RequestType::NodeList)))
                    })
            })
            .into_future()
            .flatten()
    }

    pub fn list_secrets(
        &mut self,
        namespace: &str,
        name: Option<&str>,
    ) -> impl Future<Item = List<api_core::Secret>, Error = Error> {
        let field_selector = name.map(|name| format!("metadata.name={}", name));
        let params = ListOptional {
            field_selector: field_selector.as_ref().map(String::as_ref),
            ..ListOptional::default()
        };
        api_core::Secret::list_namespaced_secret(namespace, params)
            .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::SecretList))))
            .map(|req| {
                self.request(req, false)
                    .and_then(|response| match response {
                        ListResponse::Ok(list) => Ok(list),
                        ListResponse::Other(_) => {
                            Err(Error::from(ErrorKind::Response(RequestType::SecretList)))
                        }
                    })
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::Response(RequestType::SecretList)))
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
        api_core::Secret::create_namespaced_secret(namespace, secret, CreateOptional::default())
            .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::SecretCreate))))
            .map(|req| {
                self.request(req, false)
                    .and_then(|response| match response {
                        CreateResponse::Accepted(s)
                        | CreateResponse::Created(s)
                        | CreateResponse::Ok(s) => Ok(s),
                        _ => Err(Error::from(ErrorKind::Response(RequestType::SecretCreate))),
                    })
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::Response(RequestType::SecretCreate)))
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
            ReplaceOptional::default(),
        )
        .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::SecretReplace))))
        .map(|req| {
            self.request(req, false)
                .and_then(|response| match response {
                    ReplaceResponse::Created(s) | ReplaceResponse::Ok(s) => Ok(s),
                    _ => Err(Error::from(ErrorKind::Response(RequestType::SecretReplace))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::SecretReplace)))
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
                ..api_auth::TokenReviewSpec::default()
            },
            ..api_auth::TokenReview::default()
        };

        api_auth::TokenReview::create_token_review(&token, CreateOptional::default())
            .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::TokenReview))))
            .map(|req| {
                self.request(req, false)
                    .and_then(|response| match response {
                        CreateResponse::Created(t) | CreateResponse::Ok(t) => Ok(t),
                        _ => Err(Error::from(ErrorKind::Response(RequestType::TokenReview))),
                    })
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::Response(RequestType::TokenReview)))
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
    ) -> impl Future<Item = List<api_core::ServiceAccount>, Error = Error> {
        let field_selector = name.map(|name| format!("metadata.name={}", name));
        let params = ListOptional {
            field_selector: field_selector.as_ref().map(String::as_ref),
            label_selector,
            ..ListOptional::default()
        };

        api_core::ServiceAccount::list_namespaced_service_account(namespace, params)
            .map_err(|err| {
                Error::from(err.context(ErrorKind::Request(RequestType::ServiceAccountList)))
            })
            .map(|req| {
                self.request(req, true)
                    .and_then(|response| match response {
                        ListResponse::Ok(list) => Ok(list),
                        ListResponse::Other(_) => Err(Error::from(ErrorKind::Response(
                            RequestType::ServiceAccountList,
                        ))),
                    })
                    .map_err(|err| {
                        Error::from(
                            err.context(ErrorKind::Response(RequestType::ServiceAccountList)),
                        )
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
            CreateOptional::default(),
        )
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Request(RequestType::ServiceAccountCreate)))
        })
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    CreateResponse::Accepted(service_account)
                    | CreateResponse::Created(service_account)
                    | CreateResponse::Ok(service_account) => Ok(service_account),
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::ServiceAccountCreate,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::ServiceAccountCreate)))
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
        .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::ServiceAccountGet))))
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    api_core::ReadNamespacedServiceAccountResponse::Ok(service_account) => {
                        Ok(service_account)
                    }
                    api_core::ReadNamespacedServiceAccountResponse::Other(_) => Err(Error::from(
                        ErrorKind::Response(RequestType::ServiceAccountGet),
                    )),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::ServiceAccountGet)))
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
            ReplaceOptional::default(),
        )
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Request(RequestType::ServiceAccountReplace)))
        })
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    ReplaceResponse::Created(service_account)
                    | ReplaceResponse::Ok(service_account) => Ok(service_account),
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::ServiceAccountReplace,
                    ))),
                })
                .map_err(|err| {
                    Error::from(
                        err.context(ErrorKind::Response(RequestType::ServiceAccountReplace)),
                    )
                })
        })
        .into_future()
        .flatten()
    }

    pub fn delete_service_account(
        &mut self,
        namespace: &str,
        name: &str,
    ) -> impl Future<Item = (), Error = Error> {
        api_core::ServiceAccount::delete_namespaced_service_account(
            name,
            namespace,
            DeleteOptional::default(),
        )
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Request(RequestType::ServiceAccountDelete)))
        })
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    DeleteResponse::OkStatus(_) | DeleteResponse::OkValue(_) => Ok(()),
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::ServiceAccountDelete,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::ServiceAccountDelete)))
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
            ReplaceOptional::default(),
        )
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Request(RequestType::RoleBindingReplace)))
        })
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    ReplaceResponse::Created(role_binding) | ReplaceResponse::Ok(role_binding) => {
                        Ok(role_binding)
                    }
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::RoleBindingReplace,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::RoleBindingReplace)))
                })
        })
        .into_future()
        .flatten()
    }

    pub fn delete_role_binding(
        &mut self,
        namespace: &str,
        name: &str,
    ) -> impl Future<Item = (), Error = Error> {
        api_rbac::RoleBinding::delete_namespaced_role_binding(
            name,
            namespace,
            DeleteOptional::default(),
        )
        .map_err(|err| Error::from(err.context(ErrorKind::Request(RequestType::RoleBindingDelete))))
        .map(|req| {
            self.request(req, true)
                .and_then(|response| match response {
                    DeleteResponse::OkStatus(_) | DeleteResponse::OkValue(_) => Ok(()),
                    _ => Err(Error::from(ErrorKind::Response(
                        RequestType::RoleBindingDelete,
                    ))),
                })
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Response(RequestType::RoleBindingDelete)))
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
        should_log_trace: bool,
    ) -> impl Future<Item = R, Error = Error> {
        let next = |response: hyper::Response<Body>, log_trace: bool| {
            let status_code = response.status();
            response
                .into_body()
                .fold(BytesMut::new(), |mut buf, chunk| {
                    buf.extend_from_slice(chunk.as_ref());
                    future::ok::<_, HyperError>(buf)
                })
                .map_err(|err| Error::from(err.context(ErrorKind::Hyper)))
                .and_then(move |buf| {
                    debug!("HTTP Status: {}", status_code);
                    if log_trace {
                        trace!("HTTP Response:\n{}", ::std::str::from_utf8(&buf).unwrap());
                    }
                    http::StatusCode::from_u16(status_code.as_u16())
                        .map_err(|err| Error::from(err.context(ErrorKind::KubeOpenApi)))
                        .and_then(|status_code| {
                            R::try_from_parts(status_code, &buf)
                                .map_err(|err| Error::from(err.context(ErrorKind::KubeOpenApi)))
                        })
                        .map(|(result, _)| result)
                        .into_future()
                })
        };

        self.execute(req)
            .and_then(move |response| next(response, should_log_trace))
    }

    fn execute(
        &mut self,
        req: http::Request<Vec<u8>>,
    ) -> impl Future<Item = hyper::Response<Body>, Error = Error> {
        let path = req
            .uri()
            .path_and_query()
            .map_or("", |p| p.as_str())
            .to_string();
        debug!("HTTP request path: {}", path);
        self.config
            .host()
            .join(&path)
            .and_then(|base_url| {
                base_url.join(req.uri().path_and_query().map_or("", |pq| pq.as_str()))
            })
            .map_err(|err| {
                Error::from(err.context(ErrorKind::UrlJoin(self.config.host().clone(), path)))
            })
            .and_then(|url| {
                // req is an http 0.2 Request but hyper uses http 0.1, so destructure req and reassemble it.

                let (req_parts, body) = req.into_parts();

                let mut builder = hyper::Request::builder();

                builder.uri(url.as_str().parse::<Uri>().context(ErrorKind::Uri(url))?);

                builder.method(match req_parts.method {
                    http::Method::DELETE => hyper::Method::DELETE,
                    http::Method::GET => hyper::Method::GET,
                    http::Method::PATCH => hyper::Method::PATCH,
                    http::Method::POST => hyper::Method::POST,
                    http::Method::PUT => hyper::Method::PUT,
                    method => {
                        let err = failure::format_err!("unrecognized http::Method {}", method);
                        return Err(err.context(ErrorKind::Hyper).into());
                    }
                });

                for (name, value) in req_parts.headers {
                    if let Some(name) = name {
                        builder.header(name.as_str(), value.as_bytes());
                    }
                }

                // add the authorization bearer token to the request if we have one
                if let Some(token) = self.config.token_source().get()? {
                    let token = format!("Bearer {}", token)
                        .parse::<HeaderValue>()
                        .context(ErrorKind::HeaderValue("Authorization".to_owned()))?;

                    builder.header(hyper::header::AUTHORIZATION, token);
                }

                let req = builder
                    .body(body.into())
                    .map_err(|err| Error::from(err.context(ErrorKind::Hyper)))?;

                let res = self
                    .client
                    .call(req)
                    .map_err(|err| Error::from(err.context(ErrorKind::Hyper)))
                    .map(|res| res.map(From::from));
                Ok(res)
            })
            .into_future()
            .flatten()
    }
}

#[cfg(test)]
mod tests {
    use std::fmt;
    use std::fmt::Display;

    use bytes::BytesMut;
    use failure::Fail;
    use futures::{future, Future, Stream};
    use hyper::service::{service_fn, Service};
    use hyper::{Body, Error as HyperError, Request, Response, StatusCode};
    use k8s_openapi::api::apps::v1 as api_apps;
    use k8s_openapi::api::core::v1 as api_core;
    use native_tls::TlsConnector;
    use tokio::runtime::current_thread::Runtime;
    use url::percent_encoding::{utf8_percent_encode, USERINFO_ENCODE_SET};
    use url::Url;

    use crate::config::{Config, TokenSource};
    use crate::error::RequestType;
    use crate::{Client, ErrorKind};

    #[derive(Clone)]
    struct TestTokenSource();

    impl TokenSource for TestTokenSource {
        type Error = super::Error;

        fn get(&self) -> Result<Option<String>, Self::Error> {
            Ok(None)
        }
    }

    const ACCESS_REVIEW_ALLOWED: &str = r##"{"spec": {}, "status": {"allowed":true}}"##;
    const ACCESS_REVIEW_DENIED: &str = r##"{"spec": {}, "status": {"allowed":false}}"##;
    const ACCESS_REVIEW_MISSING: &str = r##"{"spec": {}}"##;

    #[test]
    fn is_subject_allowed_is_true() {
        let service = service_fn(
            move |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let mut res = Response::new(Body::from(ACCESS_REVIEW_ALLOWED));
                *res.status_mut() = StatusCode::CREATED;
                Ok(res)
            },
        );
        let mut client = make_test_client(service);

        let fut = client
            .is_subject_allowed("nodes".to_string(), "list".to_string())
            .map(|status| assert!(status.allowed));

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn is_subject_allowed_is_false() {
        let service = service_fn(
            move |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let mut res = Response::new(Body::from(ACCESS_REVIEW_DENIED));
                *res.status_mut() = StatusCode::CREATED;
                Ok(res)
            },
        );
        let mut client = make_test_client(service);

        let fut = client
            .is_subject_allowed("nodes".to_string(), "list".to_string())
            .map(|status| assert!(!status.allowed));

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn is_subject_allowed_is_default_false() {
        let service = service_fn(
            move |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let mut res = Response::new(Body::from(ACCESS_REVIEW_MISSING));
                *res.status_mut() = StatusCode::CREATED;
                Ok(res)
            },
        );
        let mut client = make_test_client(service);

        let fut = client
            .is_subject_allowed("nodes".to_string(), "list".to_string())
            .map(|status| assert!(!status.allowed));

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
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
                let res = Response::builder()
                    .status(StatusCode::SERVICE_UNAVAILABLE)
                    .body(Body::empty())
                    .unwrap();
                Ok(res)
            },
        );

        let mut client = make_test_client(service);

        let fut = client.list_pods(NAMESPACE, Some(LABEL_SELECTOR));

        if let Err(err) = Runtime::new().unwrap().block_on(fut) {
            assert_eq!(err.kind(), &ErrorKind::Response(RequestType::PodList))
        } else {
            panic!("Expected and error result")
        }
    }

    #[test]
    fn list_pods_service_error() {
        const NAMESPACE: &str = "custom-namespace";
        const LABEL_SELECTOR: &str = "x=y";
        let service = service_fn(
            |_req: Request<Body>| -> std::result::Result<Response<Body>, _> {
                Err(TestError("Some terrible error").compat())
            },
        );

        let mut client = make_test_client(service);

        let fut = client.list_pods(NAMESPACE, Some(LABEL_SELECTOR));

        if let Err(err) = Runtime::new().unwrap().block_on(fut) {
            assert_eq!(err.kind(), &ErrorKind::Response(RequestType::PodList))
        } else {
            panic!("Expected and error result")
        }
    }

    const LIST_NODE_RESPONSE: &str = r###"{
            "kind" : "NodeList",
            "items" : [
                {
                    "kind" : "Node"
                },
                {
                    "kind" : "Node"
                }
            ]
        }"###;

    #[test]
    fn list_nodes_success() {
        let service = service_fn(
            |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                Ok(Response::new(Body::from(LIST_NODE_RESPONSE)))
            },
        );

        let mut client = make_test_client(service);

        let fut = client.list_nodes().map(|nodes| {
            assert_eq!(2, nodes.items.len());
        });

        Runtime::new()
            .unwrap()
            .block_on(fut)
            .expect("Expected future to be OK");
    }

    #[test]
    fn list_nodes_error_response() {
        let service = service_fn(
            |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let res = Response::builder()
                    .status(StatusCode::SERVICE_UNAVAILABLE)
                    .body(Body::empty())
                    .unwrap();
                Ok(res)
            },
        );

        let mut client = make_test_client(service);
        let fut = client.list_nodes();

        if let Err(err) = Runtime::new().unwrap().block_on(fut) {
            assert_eq!(err.kind(), &ErrorKind::Response(RequestType::NodeList))
        } else {
            panic!("Expected and error result")
        }
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

    #[derive(Debug, Fail)]
    struct TestError(&'static str);

    impl Display for TestError {
        fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
            write!(f, "{}", self.0)
        }
    }
}
