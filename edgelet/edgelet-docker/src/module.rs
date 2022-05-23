// Copyright (c) Microsoft. All rights reserved.

use std::str::FromStr;

use anyhow::Context;
use chrono::prelude::*;
use futures::Future;
use hyper::client::connect::Connect;

use docker::models::{InlineResponse2001, InlineResponse200State};
use edgelet_core::{
    Module, ModuleOperation, ModuleRuntimeState, ModuleStatus, ModuleTop, RuntimeOperation,
};
use edgelet_utils::ensure_not_empty;

use crate::client::DockerClient;
use crate::config::DockerConfig;
use crate::error::Error;

type Deserializer = &'static mut serde_json::Deserializer<serde_json::de::IoRead<std::io::Empty>>;

pub const MODULE_TYPE: &str = "docker";
pub const MIN_DATE: &str = "0001-01-01T00:00:00Z";

pub struct DockerModule<C: Connect + 'static> {
    client: DockerClient<C>,
    name: String,
    config: DockerConfig,
}

impl<C> std::fmt::Debug for DockerModule<C>
where
    C: Connect + 'static,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("DockerModule").finish()
    }
}

impl<C: Connect + 'static> DockerModule<C> {
    pub fn new(
        client: DockerClient<C>,
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

pub trait DockerModuleTop {
    type ModuleTopFuture: Future<Item = ModuleTop, Error = anyhow::Error> + Send;

    fn top(&self) -> Self::ModuleTopFuture;
}

impl<C: 'static + Connect> DockerModuleTop for DockerModule<C> {
    type ModuleTopFuture = Box<dyn Future<Item = ModuleTop, Error = anyhow::Error> + Send>;

    fn top(&self) -> Self::ModuleTopFuture {
        let id = self.name.to_string();
        Box::new(
            self.client
                .container_api()
                .container_top(&id, "")
                .then(|result| match result {
                    Ok(resp) => {
                        let p = parse_top_response::<Deserializer>(&resp).with_context(|| {
                            Error::RuntimeOperation(RuntimeOperation::TopModule(id.clone()))
                        })?;
                        Ok(ModuleTop::new(id, p))
                    }
                    Err(err) => {
                        let err = anyhow::anyhow!(Error::from(err))
                            .context(Error::RuntimeOperation(RuntimeOperation::TopModule(id)));
                        Err(err)
                    }
                }),
        )
    }
}

fn parse_top_response<'de, D>(resp: &InlineResponse2001) -> std::result::Result<Vec<i32>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let titles = resp
        .titles()
        .ok_or_else(|| serde::de::Error::missing_field("Titles"))?;
    let pid_index = titles
        .iter()
        .position(|ref s| s.as_str() == "PID")
        .ok_or_else(|| {
            serde::de::Error::invalid_value(
                serde::de::Unexpected::Seq,
                &"array including the column title 'PID'",
            )
        })?;
    let processes = resp
        .processes()
        .ok_or_else(|| serde::de::Error::missing_field("Processes"))?;
    let pids: std::result::Result<_, _> = processes
        .iter()
        .map(|ref p| {
            let val = p.get(pid_index).ok_or_else(|| {
                serde::de::Error::invalid_length(
                    p.len(),
                    &&*format!("at least {} columns", pid_index + 1),
                )
            })?;
            let pid = val.parse::<i32>().map_err(|_| {
                serde::de::Error::invalid_value(
                    serde::de::Unexpected::Str(val),
                    &"a process ID number",
                )
            })?;
            Ok(pid)
        })
        .collect();
    Ok(pids?)
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
                _ => Some(ModuleStatus::Unknown),
            })
            .unwrap_or_else(|| ModuleStatus::Unknown);
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

impl<C: 'static + Connect> Module for DockerModule<C> {
    type Config = DockerConfig;
    type RuntimeStateFuture =
        Box<dyn Future<Item = ModuleRuntimeState, Error = anyhow::Error> + Send>;

    fn name(&self) -> &str {
        &self.name
    }

    fn type_(&self) -> &str {
        MODULE_TYPE
    }

    fn config(&self) -> &Self::Config {
        &self.config
    }

    fn runtime_state(&self) -> Self::RuntimeStateFuture {
        Box::new(
            self.client
                .container_api()
                .container_inspect(&self.name, false)
                .map(|resp| runtime_state(resp.id(), resp.state()))
                .map_err(|err| {
                    anyhow::anyhow!(Error::from(err))
                        .context(Error::ModuleOperation(ModuleOperation::RuntimeState))
                }),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::{parse_top_response, Deserializer, InlineResponse2001, Utc, MIN_DATE};

    use std::string::ToString;

    use hyper::Client;
    use serde::Serialize;
    use time::Duration;

    use docker::apis::client::APIClient;
    use docker::apis::configuration::Configuration;
    use docker::models::{ContainerCreateBody, InlineResponse200, InlineResponse200State};
    use edgelet_core::{Module, ModuleStatus};
    use edgelet_test_utils::JsonConnector;

    use crate::client::DockerClient;
    use crate::config::DockerConfig;
    use crate::module::DockerModule;

    fn create_api_client<T: Serialize>(body: T) -> DockerClient<JsonConnector> {
        let client = Client::builder().build(JsonConnector::ok(&body));

        let config = Configuration::new(client);

        DockerClient::new(APIClient::new(config))
    }

    #[test]
    fn new_instance() {
        let docker_module = DockerModule::new(
            create_api_client("boo"),
            "mod1".to_string(),
            DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
                .unwrap(),
        )
        .unwrap();

        assert_eq!("mod1", docker_module.name());
        assert_eq!("docker", docker_module.type_());
        assert_eq!("ubuntu", docker_module.config().image());
    }

    #[test]
    fn empty_name_fails() {
        let _ = DockerModule::new(
            create_api_client("boo"),
            "".to_string(),
            DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
                .unwrap(),
        )
        .unwrap_err();
    }

    #[test]
    fn white_space_name_fails() {
        let _ = DockerModule::new(
            create_api_client("boo"),
            "     ".to_string(),
            DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
                .unwrap(),
        )
        .unwrap_err();
    }

    fn get_inputs() -> Vec<(&'static str, i64, ModuleStatus)> {
        vec![
            ("created", 0, ModuleStatus::Stopped),
            ("paused", 0, ModuleStatus::Stopped),
            ("restarting", 0, ModuleStatus::Stopped),
            ("removing", 0, ModuleStatus::Stopped),
            ("dead", 0, ModuleStatus::Dead),
            ("exited", 0, ModuleStatus::Stopped),
            ("removing", -1, ModuleStatus::Failed),
            ("dead", -2, ModuleStatus::Dead),
            ("exited", -42, ModuleStatus::Failed),
            ("running", 0, ModuleStatus::Running),
        ]
    }

    #[test]
    fn module_status() {
        let inputs = get_inputs();

        for &(docker_status, exit_code, ref module_status) in &inputs {
            let docker_module = DockerModule::new(
                create_api_client(
                    InlineResponse200::new().with_state(
                        InlineResponse200State::new()
                            .with_status(docker_status.to_string())
                            .with_exit_code(exit_code),
                    ),
                ),
                "mod1".to_string(),
                DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
                    .unwrap(),
            )
            .unwrap();

            let state = tokio::runtime::current_thread::Runtime::new()
                .unwrap()
                .block_on(docker_module.runtime_state())
                .unwrap();
            assert_eq!(module_status, state.status());
        }
    }

    #[test]
    fn module_runtime_state() {
        let started_at = Utc::now().to_rfc3339();
        let finished_at = (Utc::now() + Duration::hours(1)).to_rfc3339();
        let docker_module = DockerModule::new(
            create_api_client(
                InlineResponse200::new()
                    .with_state(
                        InlineResponse200State::new()
                            .with_exit_code(10)
                            .with_status("running".to_string())
                            .with_started_at(started_at.clone())
                            .with_finished_at(finished_at.clone())
                            .with_pid(1234),
                    )
                    .with_id("mod1".to_string())
                    .with_exec_i_ds(vec!["id1".to_string(), "id2".to_string()]),
            ),
            "mod1".to_string(),
            DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
                .unwrap(),
        )
        .unwrap();

        let runtime_state = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(docker_module.runtime_state())
            .unwrap();

        assert_eq!(ModuleStatus::Running, *runtime_state.status());
        assert_eq!(10, runtime_state.exit_code().unwrap());
        assert_eq!(&"running", &runtime_state.status_description().unwrap());
        assert_eq!(started_at, runtime_state.started_at().unwrap().to_rfc3339());
        assert_eq!(
            finished_at,
            runtime_state.finished_at().unwrap().to_rfc3339()
        );
        assert_eq!(Some(1234), runtime_state.pid());
    }

    #[test]
    fn module_runtime_state_dead() {
        let started_at = Utc::now().to_rfc3339();
        let finished_at = (Utc::now() + Duration::hours(1)).to_rfc3339();
        let docker_module = DockerModule::new(
            create_api_client(
                InlineResponse200::new()
                    .with_state(
                        InlineResponse200State::new()
                            .with_exit_code(10)
                            .with_status("dead".to_string())
                            .with_started_at(started_at.clone())
                            .with_finished_at(finished_at.clone()),
                    )
                    .with_id("mod1".to_string()),
            ),
            "mod1".to_string(),
            DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
                .unwrap(),
        )
        .unwrap();

        let runtime_state = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(docker_module.runtime_state())
            .unwrap();

        assert_eq!(ModuleStatus::Dead, *runtime_state.status());
        assert_eq!(10, runtime_state.exit_code().unwrap());
        assert_eq!(&"dead", &runtime_state.status_description().unwrap());
        assert_eq!(started_at, runtime_state.started_at().unwrap().to_rfc3339());
        assert_eq!(
            finished_at,
            runtime_state.finished_at().unwrap().to_rfc3339()
        );
    }

    #[test]
    fn module_runtime_state_with_bad_started_at() {
        let started_at = "not really a date".to_string();
        let finished_at = (Utc::now() + Duration::hours(1)).to_rfc3339();
        let docker_module = DockerModule::new(
            create_api_client(
                InlineResponse200::new()
                    .with_state(
                        InlineResponse200State::new()
                            .with_exit_code(10)
                            .with_status("running".to_string())
                            .with_started_at(started_at)
                            .with_finished_at(finished_at),
                    )
                    .with_id("mod1".to_string()),
            ),
            "mod1".to_string(),
            DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
                .unwrap(),
        )
        .unwrap();

        let runtime_state = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(docker_module.runtime_state())
            .unwrap();

        assert_eq!(None, runtime_state.started_at());
    }

    #[test]
    fn module_runtime_state_with_bad_finished_at() {
        let started_at = Utc::now().to_rfc3339();
        let finished_at = "nope, not a date".to_string();
        let docker_module = DockerModule::new(
            create_api_client(
                InlineResponse200::new()
                    .with_state(
                        InlineResponse200State::new()
                            .with_exit_code(10)
                            .with_status("running".to_string())
                            .with_started_at(started_at)
                            .with_finished_at(finished_at),
                    )
                    .with_id("mod1".to_string()),
            ),
            "mod1".to_string(),
            DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
                .unwrap(),
        )
        .unwrap();

        let runtime_state = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(docker_module.runtime_state())
            .unwrap();

        assert_eq!(None, runtime_state.finished_at());
    }

    #[test]
    fn module_runtime_state_with_min_dates() {
        let started_at = MIN_DATE.to_string();
        let finished_at = MIN_DATE.to_string();
        let docker_module = DockerModule::new(
            create_api_client(
                InlineResponse200::new()
                    .with_state(
                        InlineResponse200State::new()
                            .with_exit_code(10)
                            .with_status("stopped".to_string())
                            .with_started_at(started_at)
                            .with_finished_at(finished_at),
                    )
                    .with_id("mod1".to_string()),
            ),
            "mod1".to_string(),
            DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
                .unwrap(),
        )
        .unwrap();

        let runtime_state = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(docker_module.runtime_state())
            .unwrap();

        assert_eq!(None, runtime_state.started_at());
        assert_eq!(None, runtime_state.finished_at());
    }

    #[test]
    fn parse_top_response_returns_pid_array() {
        let response = InlineResponse2001::new()
            .with_titles(vec!["PID".to_string()])
            .with_processes(vec![vec!["123".to_string()]]);

        let pids = parse_top_response::<Deserializer>(&response);

        assert_eq!(vec![123], pids.unwrap());
    }

    #[test]
    fn parse_top_response_returns_error_when_titles_is_missing() {
        let response = InlineResponse2001::new().with_processes(vec![vec!["123".to_string()]]);

        let pids = parse_top_response::<Deserializer>(&response);

        assert_eq!("missing field `Titles`", format!("{}", pids.unwrap_err()));
    }

    #[test]
    fn parse_top_response_returns_error_when_pid_title_is_missing() {
        let response = InlineResponse2001::new().with_titles(vec!["Command".to_string()]);

        let pids = parse_top_response::<Deserializer>(&response);

        assert_eq!(
            "invalid value: sequence, expected array including the column title 'PID'",
            format!("{}", pids.unwrap_err())
        );
    }

    #[test]
    fn parse_top_response_returns_error_when_processes_is_missing() {
        let response = InlineResponse2001::new().with_titles(vec!["PID".to_string()]);

        let pids = parse_top_response::<Deserializer>(&response);

        assert_eq!(
            "missing field `Processes`",
            format!("{}", pids.unwrap_err())
        );
    }

    #[test]
    fn parse_top_response_returns_error_when_process_pid_is_missing() {
        let response = InlineResponse2001::new()
            .with_titles(vec!["Command".to_string(), "PID".to_string()])
            .with_processes(vec![vec!["sh".to_string()]]);

        let pids = parse_top_response::<Deserializer>(&response);

        assert_eq!(
            "invalid length 1, expected at least 2 columns",
            format!("{}", pids.unwrap_err())
        );
    }

    #[test]
    fn parse_top_response_returns_error_when_process_pid_is_not_i32() {
        let response = InlineResponse2001::new()
            .with_titles(vec!["PID".to_string()])
            .with_processes(vec![vec!["xyz".to_string()]]);

        let pids = parse_top_response::<Deserializer>(&response);

        assert_eq!(
            "invalid value: string \"xyz\", expected a process ID number",
            format!("{}", pids.unwrap_err())
        );
    }
}
