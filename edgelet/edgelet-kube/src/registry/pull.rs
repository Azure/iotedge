// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::convert::TryFrom;

use failure::Fail;
use futures::future::Either;
use futures::prelude::*;
use futures::{future, Future, Stream};
use hyper::service::Service;
use hyper::Body;

use edgelet_docker::DockerConfig;
use kube_client::TokenSource;

use crate::convert::NamedSecret;
use crate::error::{Error, ErrorKind};
use crate::registry::ImagePullSecret;
use crate::KubeModuleRuntime;

pub fn create_image_pull_secrets<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    config: &DockerConfig,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
    S::Future: Send,
{
    let image_pull_secrets = vec![config.auth(), runtime.settings().proxy().auth()]
        .into_iter()
        .filter_map(|auth| {
            auth.and_then(|auth| ImagePullSecret::from_auth(&auth))
                .map(|secret| (secret.name(), secret))
        })
        .collect::<HashMap<_, _>>()
        .drain()
        .map(|(_, secret)| secret)
        .collect::<Vec<_>>();

    create_or_update_image_pull_secrets(runtime, image_pull_secrets)
        .map_err(|err| Error::from(err.context(ErrorKind::RegistryOperation)))
}

fn create_or_update_image_pull_secrets<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    image_pull_secrets: impl IntoIterator<Item = ImagePullSecret>,
) -> impl Future<Item = (), Error = Error> + 'static
where
    T: TokenSource + Send + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
    S::Future: Send,
{
    let client_copy = runtime.client();
    let namespace_copy = runtime.settings().namespace().to_owned();

    let named_secrets: Vec<_> = image_pull_secrets
        .into_iter()
        .map(|secret| NamedSecret::try_from((runtime.settings().namespace().into(), secret)))
        .collect();

    runtime
        .client()
        .lock()
        .expect("Unexpected lock error")
        .borrow_mut()
        .list_secrets(runtime.settings().namespace(), None)
        .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
        .and_then(move |secrets| {
            let existing_names: Vec<_> = secrets
                .items
                .into_iter()
                .filter_map(|secret| secret.metadata.and_then(|metadata| metadata.name))
                .collect();

            let futures = named_secrets.into_iter().map(move |pull_secret| {
                pull_secret
                    .map(|pull_secret| {
                        if existing_names.iter().any(|name| name == pull_secret.name()) {
                            let f = client_copy
                                .lock()
                                .expect("Unexpected lock error")
                                .borrow_mut()
                                .replace_secret(
                                    namespace_copy.as_str(),
                                    pull_secret.name(),
                                    pull_secret.secret(),
                                )
                                .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
                                .map(|_| ());
                            Either::A(f)
                        } else {
                            let f = client_copy
                                .lock()
                                .expect("Unexpected lock error")
                                .borrow_mut()
                                .create_secret(namespace_copy.as_str(), pull_secret.secret())
                                .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
                                .map(|_| ());
                            Either::B(f)
                        }
                    })
                    .into_future()
                    .flatten()
            });

            future::join_all(futures).map(|_| ())
        })
}

#[cfg(test)]
mod tests {
    use hyper::service::service_fn;
    use hyper::{Body, Method, Request, StatusCode};
    use maplit::btreemap;
    use serde_json::json;
    use tokio::runtime::Runtime;

    use docker::models::{AuthConfig, ContainerCreateBody};
    use edgelet_docker::DockerConfig;
    use edgelet_test_utils::routes;
    use edgelet_test_utils::web::{
        make_req_dispatcher, HttpMethod, RequestHandler, RequestPath, ResponseFuture,
    };

    use crate::error::PullImageErrorReason;
    use crate::registry::pull::create_or_update_image_pull_secrets;
    use crate::registry::{create_image_pull_secrets, ImagePullSecret};
    use crate::tests::{create_runtime, make_settings, not_found_handler, response};
    use crate::ErrorKind;

    #[test]
    fn it_creates_image_pull_secret_if_it_does_not_exist() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/secrets", settings.namespace()) => empty_secret_list_handler(),
            POST format!("/api/v1/namespaces/{}/secrets", settings.namespace()) => create_secret_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let image_pull_secrets = vec![ImagePullSecret::default()
            .with_registry("REGISTRY")
            .with_username("USER")
            .with_password("PASSWORD")];

        let task = create_or_update_image_pull_secrets(&runtime, image_pull_secrets);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_updates_image_pull_secret_if_it_exists() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/secrets", settings.namespace()) => secret_list_handler(),
            PUT format!("/api/v1/namespaces/{}/secrets/user-registry", settings.namespace(), ) => replace_secret_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let image_pull_secrets = vec![ImagePullSecret::default()
            .with_registry("REGISTRY")
            .with_username("USER")
            .with_password("PASSWORD")];

        let task = create_or_update_image_pull_secrets(&runtime, image_pull_secrets);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_returns_an_error_when_image_pull_secret_is_invalid() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/secrets", settings.namespace()) => secret_list_handler(),
            PUT format!("/api/v1/namespaces/{}/secrets/user-registry", settings.namespace(), ) => replace_secret_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let image_pull_secrets = vec![ImagePullSecret::default()];

        let task = create_or_update_image_pull_secrets(&runtime, image_pull_secrets);

        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();
        assert_eq!(
            err.kind(),
            &ErrorKind::PullImage(PullImageErrorReason::AuthName)
        )
    }

    #[test]
    fn it_updates_image_pull_secrets_for_both_module_and_proxy() {
        let settings = make_settings(Some(json!({
            "proxy": {
                "auth": {
                    "username": "user",
                    "password": "password",
                    "serveraddress": "registry2"
                },
            },
        })));

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/secrets", settings.namespace()) => secret_list_handler(),
            PUT format!("/api/v1/namespaces/{}/secrets/user-registry", settings.namespace()) => replace_secret_handler(),
            PUT format!("/api/v1/namespaces/{}/secrets/user-registry2", settings.namespace()) => replace_secret_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let auth_config = AuthConfig::new()
            .with_password(String::from("PASSWORD"))
            .with_username(String::from("USER"))
            .with_serveraddress(String::from("REGISTRY"));

        let config = DockerConfig::new(
            "my-image:v1.0".to_string(),
            ContainerCreateBody::new(),
            Some(auth_config),
        )
        .unwrap();

        let task = create_image_pull_secrets(&runtime, &config);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_creates_image_pull_secret_only_once_if_both_module_and_proxy_stored_in_the_same_repo() {
        let settings = make_settings(Some(json!({
            "proxy": {
                "auth": {
                    "username": "user",
                    "password": "password",
                    "serveraddress": "registry"
                },
            },
        })));

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/secrets", settings.namespace()) => secret_list_handler(),
            PUT format!("/api/v1/namespaces/{}/secrets/user-registry", settings.namespace()) => replace_secret_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let auth_config = AuthConfig::new()
            .with_password(String::from("PASSWORD"))
            .with_username(String::from("USER"))
            .with_serveraddress(String::from("REGISTRY"));

        let config = DockerConfig::new(
            "my-image:v1.0".to_string(),
            ContainerCreateBody::new(),
            Some(auth_config),
        )
        .unwrap();

        let task = create_image_pull_secrets(&runtime, &config);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    fn empty_secret_list_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "SecretList",
                    "apiVersion": "v1",
                    "items": []
                })
                .to_string()
            })
        }
    }

    fn secret_list_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                   "kind": "SecretList",
                    "apiVersion": "v1",
                    "items": [
                        {
                            "metadata": {
                                "name": "user-registry",
                                "namespace": "my-namespace",
                            }
                        },
                        {
                            "metadata": {
                                "name": "user-registry2",
                                "namespace": "my-namespace",
                            }
                        }
                    ]
                })
                .to_string()
            })
        }
    }

    fn create_secret_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::CREATED, || {
                json!({
                    "kind": "Secret",
                    "apiVersion": "v1",
                    "metadata": {
                        "name": "user-registry",
                        "namespace": "my-namespace",
                    },
                })
                .to_string()
            })
        }
    }

    fn replace_secret_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "Secret",
                    "apiVersion": "v1",
                    "metadata": {
                        "name": "user-registry",
                        "namespace": "my-namespace",
                    },
                })
                .to_string()
            })
        }
    }
}
