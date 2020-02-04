// Copyright (c) Microsoft. All rights reserved.

use failure::{Compat, Fail, ResultExt};
use futures::sync::mpsc::UnboundedSender;
use futures::{future, Future};

use hyper::service::{NewService, Service};
use hyper::{Body, Request};
use lazy_static::lazy_static;
use serde::de::DeserializeOwned;
use serde::Serialize;

use edgelet_core::{
    Authenticator, IdentityManager, Module, ModuleRuntime, ModuleRuntimeErrorReason, Policy,
};
use edgelet_http::authentication::Authentication;
use edgelet_http::authorization::Authorization;
use edgelet_http::route::*;
use edgelet_http::router;
use edgelet_http::Version;

mod device_actions;
mod identity;
mod module;
mod system_info;

use self::device_actions::*;
use self::identity::*;
pub use self::module::*;
use self::system_info::*;
use crate::error::{Error, ErrorKind};

lazy_static! {
    static ref AGENT_NAME: String = "edgeAgent".to_string();
}

#[derive(Clone)]
pub struct ManagementService {
    inner: RouterService<RegexRecognizer>,
}

impl ManagementService {
    pub fn new<M, I>(
        runtime: &M,
        identity: &I,
        initiate_shutdown_and_reprovision: UnboundedSender<()>,
    ) -> impl Future<Item = Self, Error = Error>
    where
        M: ModuleRuntime + Authenticator<Request = Request<Body>> + Clone + Send + Sync + 'static,
        for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
        <M::Module as Module>::Config: DeserializeOwned + Serialize,
        M::Logs: Into<Body>,
        I: IdentityManager + Clone + Send + Sync + 'static,
        I::Identity: Serialize,
        <M::AuthenticateFuture as Future>::Error: Fail,
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

            get     Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/identities"                        => ListIdentities::new(identity.clone()),
            post    Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/identities"                        => CreateIdentity::new(identity.clone()),
            put     Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/identities/(?P<name>[^/]+)"        => UpdateIdentity::new(identity.clone()),
            delete  Version2018_06_28 runtime Policy::Module(&*AGENT_NAME)  => "/identities/(?P<name>[^/]+)"        => DeleteIdentity::new(identity.clone()),

            get     Version2018_06_28 runtime Policy::Anonymous             => "/systeminfo"                        => GetSystemInfo::new(runtime.clone()),
            get     Version2019_11_05 runtime Policy::Anonymous             => "/systeminfo/resources"              => GetSystemResources::new(runtime.clone()),

            post    Version2019_10_22 runtime Policy::Module(&*AGENT_NAME)  => "/device/reprovision"                => ReprovisionDevice::new(initiate_shutdown_and_reprovision),
        );

        router.new_service().then(|inner| {
            let inner = inner.context(ErrorKind::StartService)?;
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
    type InitError = Compat<Error>;

    fn new_service(&self) -> Self::Future {
        future::ok(self.clone())
    }
}
