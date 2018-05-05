// Copyright (c) Microsoft. All rights reserved.

use error::Error;
use futures::{future, Future};
use identity::{IdentityManager, IdentitySpec};
use module::{Module, ModuleRegistry, ModuleRuntime, ModuleSpec, ModuleStatus};

pub struct Watchdog<M, I> {
    runtime: M,
    id_mgr: I,
}

impl<M, I> Watchdog<M, I>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
    I: 'static + IdentityManager,
    I::Error: Into<Error>,
{
    pub fn new(runtime: M, id_mgr: I) -> Self {
        Watchdog { runtime, id_mgr }
    }

    // Start the edge runtime module (EdgeAgent). This also updates the identity of the module (module_id)
    // to make sure it is configured for the right authentication type (sas token)
    // spec.name = edgeAgent / module_id = $edgeAgent
    pub fn start(
        &mut self,
        spec: ModuleSpec<<M::Module as Module>::Config>,
        module_id: &str,
    ) -> Box<Future<Item = (), Error = Error>> {
        let runtime = self.runtime.clone();
        let name = spec.name().to_string();

        // Update identity of EdgeAgent to use the Auth mechanism supported by this Edgelet (Sas tokens)
        let f = update_identity(&mut self.id_mgr, module_id).and_then(move |_| {
            runtime.list().map_err(|e| e.into()).and_then(move |m| {
                m.iter()
                    .filter_map(|m| if m.name() == name { Some(m) } else { None })
                    .nth(0)
                    .map(|m| start_runtime(runtime.clone(), m))
                    .unwrap_or_else(|| create_and_start(runtime, spec))
                    .map(|_| info!("Edge runtime started."))
            })
        });
        Box::new(f)
    }
}

// Update the edge agent identity to use the right authentication mechanism.
fn update_identity<I>(id_mgr: &mut I, module_id: &str) -> Box<Future<Item = (), Error = Error>>
where
    I: 'static + IdentityManager,
    I::Error: Into<Error>,
{
    info!("Updating identity for {}", module_id);
    Box::new(
        id_mgr
            .update(IdentitySpec::new(module_id))
            .map_err(|e| e.into())
            .map(|_| ()),
    )
}

// Edge agent does not exist - pull, create and start the container
fn create_and_start<M>(
    runtime: M,
    spec: ModuleSpec<<M::Module as Module>::Config>,
) -> Box<Future<Item = (), Error = Error>>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
{
    let module_name = spec.name().to_string();
    info!("Creating and starting edge runtime module {}", module_name);
    let runtime_copy = runtime.clone();
    let create_future = runtime
        .registry()
        .pull(spec.clone().config())
        .and_then(move |_| runtime.create(spec))
        .and_then(move |_| runtime_copy.start(&module_name))
        .map_err(|e| e.into());
    Box::new(create_future)
}

// Check edge agent state and start container if not running.
fn start_runtime<M>(
    runtime: M,
    module: &<M as ModuleRuntime>::Module,
) -> Box<Future<Item = (), Error = Error>>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: Clone,
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
{
    let module_name = module.name().to_string();
    let start_future = module
        .runtime_state()
        .map_err(|e| e.into())
        .and_then(move |state| match *state.status() {
            ModuleStatus::Running => future::Either::A(future::ok(())),
            _ => {
                info!("Starting edge runtime module {}", module_name);
                future::Either::B(runtime.start(&module_name).map_err(|e| e.into()))
            }
        });
    Box::new(start_future)
}
