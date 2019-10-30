// Copyright (c) Microsoft. All rights reserved.

use std::collections::hash_map::Values;
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
        .filter_map(|auth| auth.and_then(|auth| ImagePullSecret::from_auth(&auth)))
        .map(|secret| (secret.name(), secret))
        .collect::<HashMap<_, _>>();

    create_or_update_image_pull_secret(runtime, image_pull_secrets.values())
        .map_err(|err| Error::from(err.context(ErrorKind::RegistryOperation)))
}

fn create_or_update_image_pull_secret<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    image_pull_secrets: Values<'_, Option<String>, ImagePullSecret<'_>>,
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
    let client_copy = runtime.client().clone();
    let namespace_copy = runtime.settings().namespace().to_owned();

    let named_secrets: Vec<_> = image_pull_secrets
        .into_iter()
        .map(|secret| NamedSecret::try_from((runtime.settings().namespace(), secret)))
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
