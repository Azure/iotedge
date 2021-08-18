// Copyright (c) Microsoft. All rights reserved.

use std::str::FromStr;

use chrono::prelude::*;

use docker::apis::{DockerApi, DockerApiClient};
use docker::models::InlineResponse200State;
use edgelet_core::{Module, ModuleOperation, ModuleRuntimeState, ModuleStatus};
use edgelet_settings::DockerConfig;

use edgelet_utils::ensure_not_empty_with_context;

use crate::error::{Error, ErrorKind, Result};

pub const MODULE_TYPE: &str = "docker";
pub const MIN_DATE: &str = "0001-01-01T00:00:00Z";

pub struct DockerModule {
    client: DockerApiClient,
    name: String,
    config: DockerConfig,
}

impl std::fmt::Debug for DockerModule {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("DockerModule").finish()
    }
}

impl DockerModule {
    pub fn new(client: DockerApiClient, name: String, config: DockerConfig) -> Result<Self> {
        ensure_not_empty_with_context(&name, || ErrorKind::InvalidModuleName(name.clone()))?;

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
                "removing" | "dead" | "exited" => status_from_exit_code(state.exit_code()),
                "running" => Some(ModuleStatus::Running),
                _ => Some(ModuleStatus::Unknown),
            }).unwrap_or(ModuleStatus::Unknown);
        ModuleRuntimeState::default()
            .with_status(status)
            .with_exit_code(state.exit_code())
            .with_status_description(state.status().map(ToOwned::to_owned))
            .with_started_at(
                state
                    .started_at()
                    .and_then(|d| if d == MIN_DATE { None } else { Some(d) })
                    .and_then(|started_at| DateTime::from_str(started_at).ok()),
            )
            .with_finished_at(
                state
                    .finished_at()
                    .and_then(|d| if d == MIN_DATE { None } else { Some(d) })
                    .and_then(|finished_at| DateTime::from_str(finished_at).ok()),
            )
            .with_image_id(id.map(ToOwned::to_owned))
            .with_pid(state.pid())
    })
}

#[async_trait::async_trait]
impl Module for DockerModule {
    type Config = DockerConfig;
    type Error = Error;

    fn name(&self) -> &str {
        &self.name
    }

    fn type_(&self) -> &str {
        MODULE_TYPE
    }

    fn config(&self) -> &Self::Config {
        &self.config
    }

    async fn runtime_state(&self) -> Result<ModuleRuntimeState> {
        let inspect = self
            .client
            .container_inspect(&self.name, false)
            .await
            .map_err(|err| {
                Error::from_docker_error(
                    err,
                    ErrorKind::ModuleOperation(ModuleOperation::RuntimeState),
                )
            })?;

        Ok(runtime_state(inspect.id(), inspect.state()))
    }
}
