// Copyright (c) Microsoft. All rights reserved.

use anyhow::Context;

use docker::apis::{DockerApi, DockerApiClient};
use docker::models::InlineResponse200State;
use edgelet_core::{Module, ModuleOperation, ModuleRuntimeState, ModuleStatus};
use edgelet_settings::DockerConfig;
use edgelet_utils::ensure_not_empty;

use crate::error::Error;

pub const MODULE_TYPE: &str = "docker";
pub const MIN_DATE: &str = "0001-01-01T00:00:00Z";

pub struct DockerModule<C> {
    client: DockerApiClient<C>,
    name: String,
    config: DockerConfig,
}

impl<C> std::fmt::Debug for DockerModule<C> {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("DockerModule").finish()
    }
}

impl<C> DockerModule<C> {
    pub fn new(
        client: DockerApiClient<C>,
        name: String,
        config: DockerConfig,
    ) -> anyhow::Result<Self> {
        ensure_not_empty(&name).with_context(|| Error::InvalidModuleName(name.clone()))?;

        Ok(DockerModule {
            client,
            name,
            config,
        })
    }
}

fn status_from_exit_code(exit_code: Option<i64>) -> Option<ModuleStatus> {
    exit_code.map(|code| {
        if code == 0 {
            ModuleStatus::Stopped
        } else {
            ModuleStatus::Failed
        }
    })
}

pub fn runtime_state(
    id: Option<&str>,
    response_state: Option<&InlineResponse200State>,
) -> ModuleRuntimeState {
    response_state.map_or_else(ModuleRuntimeState::default, |state| {
        let status = state
            .status()
            .and_then(|status| match status {
                "created" | "paused" | "restarting" => Some(ModuleStatus::Stopped),
                "removing" | "exited" => status_from_exit_code(state.exit_code()),
                "dead" => Some(ModuleStatus::Dead),
                "running" => Some(ModuleStatus::Running),
                _ => None,
            })
            .unwrap_or_default();
        ModuleRuntimeState::default()
            .with_status(status)
            .with_exit_code(state.exit_code())
            .with_started_at(
                state
                    .started_at()
                    .and_then(|d| if d == MIN_DATE { None } else { Some(d) })
                    .and_then(|started_at| started_at.parse().ok()),
            )
            .with_finished_at(
                state
                    .finished_at()
                    .and_then(|d| if d == MIN_DATE { None } else { Some(d) })
                    .and_then(|finished_at| finished_at.parse().ok()),
            )
            .with_image_id(id.map(ToOwned::to_owned))
            .with_pid(state.pid())
    })
}

#[async_trait::async_trait]
impl<C> Module for DockerModule<C>
where
    C: Clone + hyper::client::connect::Connect + Send + Sync + 'static,
{
    type Config = DockerConfig;

    fn name(&self) -> &str {
        &self.name
    }

    fn type_(&self) -> &str {
        MODULE_TYPE
    }

    fn config(&self) -> &Self::Config {
        &self.config
    }

    async fn runtime_state(&self) -> anyhow::Result<ModuleRuntimeState> {
        let inspect = self
            .client
            .container_inspect(&self.name, false)
            .await
            .context(Error::Docker)
            .context(Error::ModuleOperation(ModuleOperation::RuntimeState))?;

        Ok(runtime_state(inspect.id(), inspect.state()))
    }
}
