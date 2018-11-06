// Copyright (c) Microsoft. All rights reserved.

use super::Policy;
use error::Error;
use futures::{future, Future};
use module::ModuleRuntime;
use pid::Pid;

pub struct Authorization<M>
where
    M: 'static + ModuleRuntime,
{
    _runtime: M,
    _policy: Policy,
}

impl<M> Authorization<M>
where
    M: 'static + ModuleRuntime,
{
    pub fn new(_runtime: M, _policy: Policy) -> Self {
        Authorization { _runtime, _policy }
    }

    pub fn authorize(
        &self,
        _name: Option<String>,
        _pid: Pid,
    ) -> impl Future<Item = bool, Error = Error> {
        // TODO: Implement this with mTLS.
        warn!("Ignoring authorization request.");
        future::ok(true)
    }
}
