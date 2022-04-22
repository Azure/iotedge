// Copyright (c) Microsoft. All rights reserved.

use anyhow::Context;
use futures::sync::mpsc::UnboundedSender;
use futures::{future, Future};
use std::sync::{Arc, Mutex};

use hyper::service::{NewService, Service};
use hyper::{Body, Request};
use lazy_static::lazy_static;
use serde::de::DeserializeOwned;
use serde::Serialize;

use edgelet_core::{Authenticator, Module, ModuleRuntime, Policy};
use edgelet_http::authentication::Authentication;
use edgelet_http::authorization::Authorization;
use edgelet_http::route::{Builder, RegexRecognizer, Router, RouterService};
use edgelet_http::router;
use edgelet_http::Version;
use identity_client::client::IdentityClient;

mod device_actions;
mod identity;
mod module;
mod system_info;

use self::device_actions::ReprovisionDevice;
use self::identity::{CreateIdentity, DeleteIdentity, ListIdentities, UpdateIdentity};
pub use self::module::*;
use self::system_info::{GetSupportBundle, GetSystemInfo, GetSystemResources};
use crate::error::Error;

lazy_static! {
    static ref AGENT_NAME: String = "edgeAgent".to_string();
}

#[derive(Clone)]
pub struct ManagementService {
    inner: RouterService<RegexRecognizer>,
}

impl ManagementService {
    pub fn new<M>(
        runtime: &M,
        identity_client: Arc<Mutex<IdentityClient>>,
        initiate_shutdown_and_reprovision: UnboundedSender<()>,
    ) -> impl Future<Item = Self, Error = anyhow::Error>
    where
        M: ModuleRuntime + Authenticator<Request = Request<Body>> + Clone + Send + Sync + 'static,
        <M::Module as Module>::Config: DeserializeOwned + Serialize,
        M::Logs: Into<Body>,
    {
        let router = router!(
            get     Version2018_06_28 runtime Policy::Anonymous             => "/modules"                           => ListModules::new(runtime.clone()),
            post    Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/modules"                           => CreateModule::new(runtime.clone()),
            get     Version2018_06_28 runtime Policy::Anonymous             => "/modules/(?P<name>[^/]+)"           => GetModule,
            put     Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/modules/(?P<name>[^/]+)"           => UpdateModule::new(runtime.clone()),
            post    Version2019_01_30 runtime Policy::Module(&*AGENT_NAME)  => "/modules/(?P<name>[^/]+)/prepareupdate"   => PrepareUpdateModule::new(runtime.clone()),
            delete  Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/modules/(?P<name>[^/]+)"           => DeleteModule::new(runtime.clone()),
            post    Version2018_06_28 runtime Policy::Anonymous             => "/modules/(?P<name>[^/]+)/start"     => StartModule::new(runtime.clone()),
            post    Version2018_06_28 runtime Policy::Anonymous             => "/modules/(?P<name>[^/]+)/stop"      => StopModule::new(runtime.clone()),
            post    Version2018_06_28 runtime Policy::Anonymous             => "/modules/(?P<name>[^/]+)/restart"   => RestartModule::new(runtime.clone()),
            get     Version2018_06_28 runtime Policy::Anonymous             => "/modules/(?P<name>[^/]+)/logs"      => ModuleLogs::new(runtime.clone()),

            get     Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/identities"                        => ListIdentities::new(identity_client.clone()),
            post    Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/identities"                        => CreateIdentity::new(identity_client.clone()),
            put     Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/identities/(?P<name>[^/]+)"        => UpdateIdentity::new(identity_client.clone()),
            delete  Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/identities/(?P<name>[^/]+)"        => DeleteIdentity::new(identity_client),

            get     Version2018_06_28 runtime Policy::Anonymous             => "/systeminfo"                        => GetSystemInfo::new(runtime.clone()),
            get     Version2019_11_05 runtime Policy::Anonymous             => "/systeminfo/resources"              => GetSystemResources::new(runtime.clone()),
            get     Version2020_07_07 runtime Policy::Anonymous             => "/systeminfo/supportbundle"          => GetSupportBundle::new(runtime.clone()),

            post    Version2019_10_22 runtime Policy::Module(&*AGENT_NAME)  => "/device/reprovision"                => ReprovisionDevice::new(initiate_shutdown_and_reprovision),
        );

        router.new_service().then(|inner| {
            let inner = inner.context(Error::StartService)?;
            Ok(ManagementService { inner })
        })
    }
}

impl Service for ManagementService {
    type ReqBody = <RouterService<RegexRecognizer> as Service>::ReqBody;
    type ResBody = <RouterService<RegexRecognizer> as Service>::ResBody;
    type Error = <RouterService<RegexRecognizer> as Service>::Error;
    type Future = <RouterService<RegexRecognizer> as Service>::Future;

    fn call(&mut self, req: Request<Body>) -> Self::Future {
        self.inner.call(req)
    }
}

impl NewService for ManagementService {
    type ReqBody = <Self::Service as Service>::ReqBody;
    type ResBody = <Self::Service as Service>::ResBody;
    type Error = <Self::Service as Service>::Error;
    type Service = Self;
    type Future = future::FutureResult<Self::Service, Self::InitError>;
    type InitError = anyhow::Error;

    fn new_service(&self) -> Self::Future {
        future::ok(self.clone())
    }
}
