// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;
use std::sync::{Arc, Mutex};

use futures::future::{Either, IntoFuture};
use futures::{future, Future, Stream};
use hyper::service::Service;
use hyper::{Body, Request};
use log::Level;
use typed_headers::{Authorization, HeaderMapExt};

use edgelet_core::AuthId;
use edgelet_utils::log_failure;
use kube_client::{Client as KubeClient, Error as KubeClientError, TokenSource};

use crate::constants::EDGE_ORIGINAL_MODULEID;
use crate::error::Error;
use crate::KubeModuleRuntime;

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
    S::Error: Into<KubeClientError>,
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
                            Error::from(err)
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
        .map_err(Error::from)
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
    S::Error: Into<KubeClientError>,
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
                    Error::from(err)
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
