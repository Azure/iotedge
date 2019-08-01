// Copyright (c) Microsoft. All rights reserved.

use crate::app;
use crate::error::Error;
use crate::signal;

#[cfg(feature = "runtime-docker")]
use edgelet_docker::DockerModuleRuntime;
#[cfg(feature = "runtime-kubernetes")]
use edgelet_kube::KubeModuleRuntime;
#[cfg(feature = "runtime-kubernetes")]
use hyper::client::HttpConnector;
#[cfg(feature = "runtime-kubernetes")]
use hyper::Body;
#[cfg(feature = "runtime-kubernetes")]
use hyper_tls::HttpsConnector;
#[cfg(feature = "runtime-kubernetes")]
use kube_client::{HttpClient, ValueToken};

#[cfg(feature = "runtime-docker")]
type ModuleRuntime = DockerModuleRuntime;
#[cfg(feature = "runtime-kubernetes")]
type ModuleRuntime = KubeModuleRuntime<ValueToken, HttpClient<HttpsConnector<HttpConnector>, Body>>;

pub fn run() -> Result<(), Error> {
    let settings = app::init::<ModuleRuntime>()?;
    let main = super::Main::<ModuleRuntime>::new(settings);

    main.run_until(signal::shutdown)?;
    Ok(())
}
