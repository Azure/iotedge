// Copyright (c) Microsoft. All rights reserved.

use std::cmp::Ordering;
use std::time::{Duration, Instant};

use failure::{Fail, ResultExt};
use futures::future::{self, Either};
use futures::Future;
use log::{info, warn, Level};
use tokio::prelude::*;
use tokio::timer::Interval;

use edgelet_core::{ImagePullPolicy, ModuleRegistry};
use edgelet_utils::log_failure;

use aziot_identity_common::Identity as AziotIdentity;
use edgelet_core::module::{
    Module, ModuleRuntime, ModuleRuntimeErrorReason, ModuleSpec, ModuleStatus,
};
use edgelet_core::settings::RetryLimit;

use crate::error::{Error, ErrorKind, InitializeErrorReason};

// Time to allow EdgeAgent to gracefully shutdown (including stopping all modules, and updating reported properties)
const EDGE_RUNTIME_STOP_TIME: Duration = Duration::from_secs(60);

/// This variable holds the generation ID associated with the Edge Agent module.
const MODULE_GENERATIONID: &str = "IOTEDGE_MODULEGENERATIONID";

/// This is the frequency with which the watchdog checks for the status of the edge runtime module.
const WATCHDOG_FREQUENCY_SECS: u64 = 60;

pub struct Watchdog<M> {
    runtime: M,
    max_retries: RetryLimit,
    identityd_url: url::Url,
}

impl<M> Watchdog<M>
where
    M: 'static + ModuleRuntime + Clone,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <M::Module as Module>::Config: Clone,
{
    pub fn new(runtime: M, max_retries: RetryLimit, identityd_url: &url::Url) -> Self {
        Watchdog {
            runtime,
            max_retries,
            identityd_url: identityd_url.clone(),
        }
    }

    // Start the edge runtime module (EdgeAgent). This also updates the identity of the module (module_id)
    // to make sure it is configured for the right authentication type (sas token)
    // spec.name = edgeAgent / module_id = $edgeAgent
    pub fn run_until<F>(
        self,
        spec: ModuleSpec<<M::Module as Module>::Config>,
        module_id: &str,
        shutdown_signal: F,
    ) -> impl Future<Item = (), Error = Error>
    where
        F: Future<Item = (), Error = ()> + 'static,
    {
        let runtime = self.runtime;
        let runtime_copy = runtime.clone();
        let name = spec.name().to_string();
        let module_id = module_id.to_string();
        let max_retries = self.max_retries;
        let identityd_url = self.identityd_url;

        let watchdog = start_watchdog(runtime, spec, module_id, max_retries, identityd_url);

        // Swallow any errors from shutdown_signal
        let shutdown_signal = shutdown_signal.then(|_| Ok(()));

        // Wait for the watchdog or shutdown futures to complete
        // Since the watchdog never completes, this will wait for the
        // shutdown signal.
        shutdown_signal
            .select(watchdog)
            .then(move |result| match result {
                Ok(((), _)) => Ok(stop_runtime(&runtime_copy, &name)),
                Err((err, _)) => Err(err),
            })
            .flatten()
    }
}

// Stop EdgeAgent
fn stop_runtime<M>(runtime: &M, name: &str) -> impl Future<Item = (), Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <M::Module as Module>::Config: Clone,
{
    info!("Stopping edge runtime module {}", name);
    runtime
        .stop(name, Some(EDGE_RUNTIME_STOP_TIME))
        .or_else(|err| match (&err).into() {
            ModuleRuntimeErrorReason::NotFound => Ok(()),
            ModuleRuntimeErrorReason::Other => {
                Err(Error::from(err.context(ErrorKind::ModuleRuntime)))
            }
        })
}

// Start watchdog on a timer for 1 minute
pub fn start_watchdog<M>(
    runtime: M,
    spec: ModuleSpec<<M::Module as Module>::Config>,
    module_id: String,
    max_retries: RetryLimit,
    identityd_url: url::Url,
) -> impl Future<Item = (), Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
{
    info!(
        "Starting watchdog with {} second frequency...",
        WATCHDOG_FREQUENCY_SECS
    );

    Interval::new(Instant::now(), Duration::from_secs(WATCHDOG_FREQUENCY_SECS))
        .map_err(|err| Error::from(err.context(ErrorKind::EdgeRuntimeStatusCheckerTimer)))
        .and_then(move |_| {
            info!("Checking edge runtime status");

            check_runtime(
                runtime.clone(),
                spec.clone(),
                module_id.clone(),
                identityd_url.clone(),
            )
            .and_then(|_| future::ok(None))
            .or_else(|e| {
                warn!("Error in watchdog when checking for edge runtime status:");
                log_failure(Level::Warn, &e);
                future::ok(Some(e))
            })
        })
        .fold(0, move |exec_count: u32, result: Option<Error>| {
            result.map_or_else(
                || Ok(0),
                |e| {
                    if max_retries.compare(exec_count) == Ordering::Greater {
                        Ok(exec_count + 1)
                    } else {
                        Err(e)
                    }
                },
            )
        })
        .map(|_| ())
}

// Check if the edge runtime module is running, and if not, start it.
fn check_runtime<M>(
    runtime: M,
    spec: ModuleSpec<<M::Module as Module>::Config>,
    module_id: String,
    identityd_url: url::Url,
) -> impl Future<Item = (), Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
{
    let module = spec.name().to_string();
    get_edge_runtime_mod(&runtime, module.clone())
        .and_then(|m| {
            m.map(|m| {
                m.runtime_state()
                    .map_err(|e| Error::from(e.context(ErrorKind::ModuleRuntime)))
            })
        })
        .and_then(move |state| match state {
            Some(state) => {
                let res = if *state.status() == ModuleStatus::Running {
                    info!("Edge runtime is running.");
                    future::Either::A(future::ok(()))
                } else {
                    info!(
                        "Edge runtime status is {}, starting module now...",
                        *state.status(),
                    );
                    future::Either::B(
                        runtime
                            .start(&module)
                            .map_err(|e| Error::from(e.context(ErrorKind::ModuleRuntime))),
                    )
                };
                Either::A(res)
            }

            None => Either::B(create_and_start(runtime, spec, &module_id, &identityd_url)),
        })
        .map(|_| ())
}

// Gets the edge runtime module, if it exists.
fn get_edge_runtime_mod<M>(
    runtime: &M,
    name: String,
) -> impl Future<Item = Option<M::Module>, Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
{
    runtime
        .list()
        .map(move |m| m.into_iter().find(move |m| m.name() == name))
        .map_err(|e| Error::from(e.context(ErrorKind::ModuleRuntime)))
}

fn create_and_start<M>(
    runtime: M,
    spec: ModuleSpec<<M::Module as Module>::Config>,
    module_id: &str,
    identityd_url: &url::Url,
) -> impl Future<Item = (), Error = Error>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
{
    let module_name = spec.name().to_string();
    info!("Creating and starting edge runtime module {}", module_name);
    let runtime_copy = runtime.clone();

    let id_mgr = identity_client::IdentityClient::new(
        aziot_identity_common_http::ApiVersion::V2020_09_01,
        &identityd_url,
    );

    id_mgr
        .update_module(module_id.as_ref())
        .then(move |identity| -> Result<_, Error> {
            let identity = identity.with_context(|_| ErrorKind::ModuleRuntime)?;

            let genid = match identity {
                AziotIdentity::Aziot(spec) => spec
                    .gen_id
                    .ok_or_else(|| Error::from(ErrorKind::ModuleRuntime))?,
                AziotIdentity::Local(_) => {
                    return Err(Error::from(ErrorKind::Initialize(
                        InitializeErrorReason::InvalidIdentityType,
                    )))
                }
            };
            Ok(genid)
        })
        .into_future()
        .and_then(move |generation_id| {
            let mut env = spec.env().clone();
            env.insert(MODULE_GENERATIONID.to_string(), generation_id.0);
            let spec = spec.with_env(env);

            let pull_future = match spec.image_pull_policy() {
                ImagePullPolicy::Never => Either::A(future::ok(())),
                ImagePullPolicy::OnCreate => Either::B(
                    runtime
                        .registry()
                        .pull(spec.config())
                        .map_err(|_| Error::from(ErrorKind::ModuleRuntime)),
                ),
            };

            pull_future
                .and_then(move |_| {
                    runtime
                        .create(spec)
                        .map_err(|e| Error::from(e.context(ErrorKind::ModuleRuntime)))
                })
                .and_then(move |_| {
                    runtime_copy
                        .start(&module_name)
                        .map_err(|e| Error::from(e.context(ErrorKind::ModuleRuntime)))
                })
        })
}
