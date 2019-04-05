// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use failure::Fail;
use futures::future::Either;
use futures::prelude::*;
use futures::{future, stream, Async, Future, Stream};
use hyper::client::HttpConnector;
use hyper::header::HeaderValue;
use hyper::service::Service;
use hyper::{header, Body, Chunk as HyperChunk, Request};
use hyper_tls::HttpsConnector;
use log::Level;

use edgelet_core::{
    AuthId, Authenticator, LogOptions, MakeModuleRuntime, ModuleRegistry, ModuleRuntime,
    ModuleRuntimeState, ModuleSpec, ProvisioningResult as CoreProvisioningResult, RuntimeOperation,
    SystemInfo,
};
use edgelet_docker::DockerConfig;
use edgelet_utils::log_failure;
use kube_client::{
    get_config, Client as KubeClient, Error as KubeClientError, HttpClient, TokenSource, ValueToken,
};
use provisioning::ProvisioningResult;

use crate::convert::{auth_to_image_pull_secret, pod_to_module, spec_to_deployment};
use crate::error::{Error, ErrorKind};
use crate::module::KubeModule;
use crate::settings::Settings;

#[derive(Clone)]
pub struct KubeModuleRuntime<T, S> {
    client: Arc<Mutex<RefCell<KubeClient<T, S>>>>,
    settings: Settings,
}

impl<T, S> KubeModuleRuntime<T, S> {
    pub fn new(client: KubeClient<T, S>, settings: Settings) -> Self {
        KubeModuleRuntime {
            client: Arc::new(Mutex::new(RefCell::new(client))),
            settings,
        }
    }
}

impl<T, S> ModuleRegistry for KubeModuleRuntime<T, S>
where
    T: TokenSource + Clone + Send + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    <S::ResBody as Stream>::Item: AsRef<[u8]>,
    <S::ResBody as Stream>::Error: Into<Error>,
    <S::ResBody as Stream>::Error: Into<KubeClientError>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    type Error = Error;
    type PullFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type RemoveFuture = Box<dyn Future<Item = (), Error = Self::Error>>;
    type Config = DockerConfig;

    fn pull(&self, config: &Self::Config) -> Self::PullFuture {
        // Find and generate image pull secrets.
        if let Some(auth) = config.auth() {
            // Have authorization for this module spec, create this if it doesn't exist.
            let fut = auth_to_image_pull_secret(self.settings.namespace(), auth)
                .map_err(Error::from)
                .map(|(secret_name, pull_secret)| {
                    let client_copy = self.client.clone();
                    let namespace_copy = self.settings.namespace().to_owned();
                    self.client
                        .lock()
                        .expect("Unexpected lock error")
                        .borrow_mut()
                        .list_secrets(self.settings.namespace(), Some(secret_name.as_str()))
                        .map_err(Error::from)
                        .and_then(move |secrets| {
                            if let Some(current_secret) = secrets.items.into_iter().find(|secret| {
                                secret.metadata.as_ref().map_or(false, |meta| {
                                    meta.name.as_ref().map_or(false, |n| *n == secret_name)
                                })
                            }) {
                                if current_secret == pull_secret {
                                    Either::A(Either::A(future::ok(())))
                                } else {
                                    let f = client_copy
                                        .lock()
                                        .expect("Unexpected lock error")
                                        .borrow_mut()
                                        .replace_secret(
                                            secret_name.as_str(),
                                            namespace_copy.as_str(),
                                            &pull_secret,
                                        )
                                        .map_err(Error::from)
                                        .map(|_| ());

                                    Either::A(Either::B(f))
                                }
                            } else {
                                let f = client_copy
                                    .lock()
                                    .expect("Unexpected lock error")
                                    .borrow_mut()
                                    .create_secret(namespace_copy.as_str(), &pull_secret)
                                    .map_err(Error::from)
                                    .map(|_| ());

                                Either::B(f)
                            }
                        })
                })
                .into_future()
                .flatten();

            Box::new(fut)
        } else {
            Box::new(future::ok(()))
        }
    }

    fn remove(&self, _: &str) -> Self::RemoveFuture {
        Box::new(future::ok(()))
    }
}

impl MakeModuleRuntime
    for KubeModuleRuntime<ValueToken, HttpClient<HttpsConnector<HttpConnector>, Body>>
{
    type Config = DockerConfig;
    type Settings = Settings;
    type ProvisioningResult = ProvisioningResult;
    type ModuleRuntime = Self;
    type Error = Error;
    type Future = Box<dyn Future<Item = Self::ModuleRuntime, Error = Self::Error> + Send>;

    fn make_runtime(
        settings: Self::Settings,
        provisioning_result: Self::ProvisioningResult,
    ) -> Self::Future {
        let settings = settings
            .with_device_id(provisioning_result.device_id())
            .with_iot_hub_hostname(provisioning_result.hub_name());
        let fut = get_config()
            .map(|config| KubeModuleRuntime::new(KubeClient::new(config), settings))
            .map_err(Error::from)
            .into_future();

        Box::new(fut)
    }
}

impl<T, S> ModuleRuntime for KubeModuleRuntime<T, S>
where
    T: TokenSource + Clone + Send + 'static,
    S: Send + Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    <S::ResBody as Stream>::Item: AsRef<[u8]>,
    <S::ResBody as Stream>::Error: Into<Error>,
    <S::ResBody as Stream>::Error: Into<KubeClientError>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    type Error = Error;
    type Config = DockerConfig;
    type Module = KubeModule;
    type ModuleRegistry = Self;
    type Chunk = Chunk;
    type Logs = Logs;

    type CreateFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type GetFuture =
        Box<dyn Future<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type ListFuture = Box<dyn Future<Item = Vec<Self::Module>, Error = Self::Error> + Send>;
    type ListWithDetailsStream =
        Box<dyn Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type LogsFuture = Box<dyn Future<Item = Self::Logs, Error = Self::Error> + Send>;
    type RemoveFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type RestartFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type StartFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type StopFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type SystemInfoFuture = Box<dyn Future<Item = SystemInfo, Error = Self::Error> + Send>;
    type RemoveAllFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;

    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        let f = spec_to_deployment(&self.settings, &module)
            .map_err(Error::from)
            .map(|(name, new_deployment)| {
                let client_copy = self.client.clone();
                let namespace_copy = self.settings.namespace().to_owned();
                self.client
                    .lock()
                    .expect("Unexpected lock error")
                    .borrow_mut()
                    .list_deployments(
                        self.settings.namespace(),
                        Some(&name),
                        Some(&self.settings.device_hub_selector()),
                    )
                    .map_err(Error::from)
                    .and_then(move |deployments| {
                        if let Some(current_deployment) =
                            deployments.items.into_iter().find(|deployment| {
                                deployment.metadata.as_ref().map_or(false, |meta| {
                                    meta.name.as_ref().map_or(false, |n| *n == name)
                                })
                            })
                        {
                            // found deployment, if the deployment found doesn't match, replace it.
                            if current_deployment == new_deployment {
                                Either::A(Either::A(future::ok(())))
                            } else {
                                let fut = client_copy
                                    .lock()
                                    .expect("Unexpected lock error")
                                    .borrow_mut()
                                    .replace_deployment(
                                        name.as_str(),
                                        namespace_copy.as_str(),
                                        &new_deployment,
                                    )
                                    .map_err(Error::from)
                                    .map(|_| ());
                                Either::A(Either::B(fut))
                            }
                        } else {
                            // Not found - create it.
                            let fut = client_copy
                                .lock()
                                .expect("Unexpected lock error")
                                .borrow_mut()
                                .create_deployment(namespace_copy.as_str(), &new_deployment)
                                .map_err(Error::from)
                                .map(|_| ());
                            Either::B(fut)
                        }
                    })
            })
            .into_future()
            .flatten();
        Box::new(f)
    }

    fn get(&self, _id: &str) -> Self::GetFuture {
        unimplemented!()
    }

    fn start(&self, _id: &str) -> Self::StartFuture {
        Box::new(future::ok(()))
    }

    fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
        Box::new(future::ok(()))
    }

    fn restart(&self, _id: &str) -> Self::RestartFuture {
        Box::new(future::ok(()))
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        Box::new(future::ok(()))
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        // TODO: Implement this.
        Box::new(future::ok(SystemInfo::new(
            "linux".to_string(),
            "x86_64".to_string(),
        )))
    }

    fn list(&self) -> Self::ListFuture {
        let result = self
            .client
            .lock()
            .expect("Unexpected lock error")
            .borrow_mut()
            .list_pods(
                self.settings.namespace(),
                Some(&self.settings.device_hub_selector()),
            )
            .map_err(Error::from)
            .and_then(|pods| {
                pods.items
                    .into_iter()
                    .filter_map(|pod| pod_to_module(&pod))
                    .try_fold(vec![], |mut modules, module_result| {
                        module_result.map(|module| {
                            modules.push(module);
                            modules
                        })
                    })
                    .into_future()
            });

        Box::new(result)
    }

    fn list_with_details(&self) -> Self::ListWithDetailsStream {
        Box::new(stream::empty())
    }

    fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
        Box::new(future::ok(Logs("".to_string(), Body::empty())))
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        self
    }

    fn remove_all(&self) -> Self::RemoveAllFuture {
        Box::new(future::ok(()))
    }
}

impl<T, S> Authenticator for KubeModuleRuntime<T, S>
where
    T: TokenSource + Clone + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    <S::ResBody as Stream>::Item: AsRef<[u8]>,
    <S::ResBody as Stream>::Error: Into<KubeClientError>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    type Error = Error;
    type Request = Request<Body>;
    type AuthenticateFuture = Box<dyn Future<Item = AuthId, Error = Self::Error> + Send>;

    fn authenticate(&self, req: &Self::Request) -> Self::AuthenticateFuture {
        let token = req
            .headers()
            .get(header::AUTHORIZATION)
            .map(HeaderValue::to_str)
            .transpose()
            .map(|token| {
                token
                    .filter(|token| token.len() > 6 && &token[..7].to_uppercase() == "BEARER ")
                    .map(|token| &token[7..])
            })
            .map_err(Error::from);

        let fut = match token {
            Ok(token) => match token {
                Some(token) => Either::A(Either::A(
                    self.client
                        .lock()
                        .expect("Unexpected lock error")
                        .borrow_mut()
                        .token_review(&self.settings.namespace(), token)
                        .map_err(|err| {
                            log_failure(Level::Warn, &err);
                            Error::from(err)
                        })
                        .map(|token_review| {
                            token_review
                                .status
                                .as_ref()
                                .filter(|status| status.authenticated.filter(|x| *x).is_some())
                                .and_then(|status| {
                                    status.user.as_ref().and_then(|user| user.username.clone())
                                })
                                .map_or(AuthId::None, |name| AuthId::Value(name.into()))
                        }),
                )),
                None => Either::A(Either::B(future::ok(AuthId::None))),
            },
            Err(e) => Either::B(future::err(e)),
        };

        Box::new(fut)
    }
}

#[derive(Debug)]
pub struct Logs(String, Body);

impl Stream for Logs {
    type Item = Chunk;
    type Error = Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        match self.1.poll() {
            Ok(Async::Ready(chunk)) => Ok(Async::Ready(chunk.map(Chunk))),
            Ok(Async::NotReady) => Ok(Async::NotReady),
            Err(err) => Err(Error::from(err.context(ErrorKind::RuntimeOperation(
                RuntimeOperation::GetModuleLogs(self.0.clone()),
            )))),
        }
    }
}

impl From<Logs> for Body {
    fn from(logs: Logs) -> Self {
        logs.1
    }
}

#[derive(Debug, Default)]
pub struct Chunk(HyperChunk);

impl IntoIterator for Chunk {
    type Item = u8;
    type IntoIter = <HyperChunk as IntoIterator>::IntoIter;

    fn into_iter(self) -> Self::IntoIter {
        self.0.into_iter()
    }
}

impl Extend<u8> for Chunk {
    fn extend<T>(&mut self, iter: T)
    where
        T: IntoIterator<Item = u8>,
    {
        self.0.extend(iter)
    }
}

impl AsRef<[u8]> for Chunk {
    fn as_ref(&self) -> &[u8] {
        self.0.as_ref()
    }
}

#[cfg(test)]
mod tests {
    use futures::future::Future;
    use hyper::service::{service_fn, Service};
    use hyper::Error as HyperError;
    use hyper::{header, Body, Request, Response};
    use native_tls::TlsConnector;
    use url::Url;

    use edgelet_core::{AuthId, Authenticator};
    use kube_client::{Client as KubeClient, Config, Error, TokenSource};

    use crate::error::ErrorKind;
    use crate::runtime::KubeModuleRuntime;
    use crate::tests::make_settings;

    #[test]
    fn authenticate_returns_none_when_no_auth_token_provided() {
        let service = service_fn(
            |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                Ok(Response::new(Body::empty()))
            },
        );
        let req = Request::default();
        let runtime = prepare_module_runtime_with_defaults(service);

        let auth_id = runtime.authenticate(&req).wait().unwrap();

        assert_eq!(AuthId::None, auth_id);
    }

    #[test]
    fn authenticate_returns_none_when_invalid_auth_header_provided() {
        let service = service_fn(
            |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                Ok(Response::new(Body::empty()))
            },
        );
        let runtime = prepare_module_runtime_with_defaults(service);

        let mut req = Request::default();
        req.headers_mut()
            .insert(header::AUTHORIZATION, "BeErer token".parse().unwrap());

        let auth_id = runtime.authenticate(&req).wait().unwrap();

        assert_eq!(AuthId::None, auth_id);
    }

    #[test]
    fn authenticate_returns_none_when_invalid_auth_token_provided() {
        let service = service_fn(
            |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                Ok(Response::new(Body::empty()))
            },
        );
        let runtime = prepare_module_runtime_with_defaults(service);

        let mut req = Request::default();
        req.headers_mut().insert(
            header::AUTHORIZATION,
            "\u{3aa}\u{3a9}\u{3a4}".parse().unwrap(),
        );

        let err = runtime.authenticate(&req).wait().err().unwrap();

        assert_eq!(&ErrorKind::ModuleAuthenticationError, err.kind());
    }

    #[test]
    fn authenticate_returns_none_when_unknown_auth_token_provided() {
        let service = service_fn(
            |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let body = r###"{
                        "kind": "TokenReview",
                        "spec": { "token": "token" },
                        "status": {
                            "authenticated": false
                        }
                    }"###;
                Ok(Response::new(Body::from(body)))
            },
        );
        let runtime = prepare_module_runtime_with_defaults(service);

        let mut req = Request::default();
        req.headers_mut().insert(
            header::AUTHORIZATION,
            "Bearer token-unknown".parse().unwrap(),
        );

        let auth_id = runtime.authenticate(&req).wait().unwrap();

        assert_eq!(AuthId::None, auth_id);
    }

    #[test]
    fn authenticate_returns_auth_id_when_module_auth_token_provided() {
        let service = service_fn(
            |_req: Request<Body>| -> Result<Response<Body>, HyperError> {
                let body = r###"{
                    "kind": "TokenReview",
                    "spec": { "token": "token" },
                    "status": {
                        "authenticated": true,
                        "user": {
                            "username": "module-abc"
                        }
                    }
                }"###;
                Ok(Response::new(Body::from(body)))
            },
        );

        let mut req = Request::default();
        req.headers_mut()
            .insert(header::AUTHORIZATION, "Bearer token".parse().unwrap());

        let runtime = prepare_module_runtime_with_defaults(service);

        let auth_id = runtime.authenticate(&req).wait().unwrap();

        assert_eq!(AuthId::Value("module-abc".into()), auth_id);
    }

    fn prepare_module_runtime_with_defaults<S: Service>(
        service: S,
    ) -> KubeModuleRuntime<TestTokenSource, S> {
        let config = Config::new(
            Url::parse("https://localhost:443").unwrap(),
            "/api".to_string(),
            TestTokenSource,
            TlsConnector::new().unwrap(),
        );

        KubeModuleRuntime::new(
            KubeClient::with_client(config, service),
            make_settings(None),
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
