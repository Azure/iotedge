// Copyright (c) Microsoft. All rights reserved.

use std::str::FromStr;

use chrono::prelude::*;
use futures::Future;
use hyper::client::connect::Connect;

use client::DockerClient;
use config::DockerConfig;
use edgelet_core::pid::Pid;
use edgelet_core::{Module, ModuleRuntimeState, ModuleStatus};
use error::{Error, Result};

pub const MODULE_TYPE: &str = "docker";
pub const MIN_DATE: &str = "0001-01-01T00:00:00Z";

pub struct DockerModule<C: Connect> {
    client: DockerClient<C>,
    name: String,
    config: DockerConfig,
}

impl<C: Connect> DockerModule<C> {
    pub fn new(
        client: DockerClient<C>,
        name: &str,
        config: DockerConfig,
    ) -> Result<DockerModule<C>> {
        Ok(DockerModule {
            client,
            name: ensure_not_empty!(name.to_string()),
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

impl<C: 'static + Connect> Module for DockerModule<C> {
    type Config = DockerConfig;
    type Error = Error;
    type RuntimeStateFuture = Box<Future<Item = ModuleRuntimeState, Error = Self::Error> + Send>;

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
                .map(|resp| {
                    resp.state()
                        .map(|state| {
                            let status = state
                                .status()
                                .and_then(|status| match status.as_ref() {
                                    "created" => Some(ModuleStatus::Stopped),
                                    "paused" => Some(ModuleStatus::Stopped),
                                    "restarting" => Some(ModuleStatus::Stopped),
                                    "removing" => status_from_exit_code(state.exit_code().cloned()),
                                    "dead" => status_from_exit_code(state.exit_code().cloned()),
                                    "exited" => status_from_exit_code(state.exit_code().cloned()),
                                    "running" => Some(ModuleStatus::Running),
                                    _ => Some(ModuleStatus::Unknown),
                                }).unwrap_or_else(|| ModuleStatus::Unknown);
                            ModuleRuntimeState::default()
                                .with_status(status)
                                .with_exit_code(state.exit_code().cloned())
                                .with_status_description(state.status().cloned())
                                .with_started_at(
                                    state
                                        .started_at()
                                        .and_then(|d| if d != MIN_DATE { Some(d) } else { None })
                                        .and_then(|started_at| DateTime::from_str(started_at).ok()),
                                ).with_finished_at(
                                    state
                                        .finished_at()
                                        .and_then(|d| if d != MIN_DATE { Some(d) } else { None })
                                        .and_then(|finished_at| {
                                            DateTime::from_str(finished_at).ok()
                                        }),
                                ).with_image_id(resp.id().cloned())
                                .with_pid(
                                    &state.pid().map(|val| Pid::Value(*val)).unwrap_or(Pid::None),
                                )
                        }).unwrap_or_else(ModuleRuntimeState::default)
                }).map_err(Error::from),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use std::string::ToString;

    use hyper::Client;
    use serde::Serialize;
    use time::Duration;
    use tokio;

    use docker::apis::client::APIClient;
    use docker::apis::configuration::Configuration;
    use docker::models::{ContainerCreateBody, InlineResponse200, InlineResponse200State};
    use edgelet_core::pid::Pid;
    use edgelet_core::{Module, ModuleStatus};
    use edgelet_test_utils::JsonConnector;

    use client::DockerClient;
    use config::DockerConfig;
    use module::DockerModule;

    fn create_api_client<T: Serialize>(body: T) -> DockerClient<JsonConnector> {
        let client = Client::builder().build(JsonConnector::new(&body));

        let mut config = Configuration::new(client);
        config.base_path = "http://localhost/".to_string();
        config.uri_composer =
            Box::new(|base_path, path| Ok(format!("{}{}", base_path, path).parse().unwrap()));

        DockerClient::new(APIClient::new(config))
    }

    #[test]
    fn new_instance() {
        let docker_module = DockerModule::new(
            create_api_client("boo"),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new(), None).unwrap(),
        ).unwrap();
        assert_eq!("mod1", docker_module.name());
        assert_eq!("docker", docker_module.type_());
        assert_eq!("ubuntu", docker_module.config().image());
    }

    #[test]
    #[should_panic]
    fn empty_name_fails() {
        let _docker_module = DockerModule::new(
            create_api_client("boo"),
            "",
            DockerConfig::new("ubuntu", ContainerCreateBody::new(), None).unwrap(),
        ).unwrap();
    }

    #[test]
    #[should_panic]
    fn white_space_name_fails() {
        let _docker_module = DockerModule::new(
            create_api_client("boo"),
            "     ",
            DockerConfig::new("ubuntu", ContainerCreateBody::new(), None).unwrap(),
        ).unwrap();
    }

    fn get_inputs() -> Vec<(&'static str, i64, ModuleStatus)> {
        vec![
            ("created", 0, ModuleStatus::Stopped),
            ("paused", 0, ModuleStatus::Stopped),
            ("restarting", 0, ModuleStatus::Stopped),
            ("removing", 0, ModuleStatus::Stopped),
            ("dead", 0, ModuleStatus::Stopped),
            ("exited", 0, ModuleStatus::Stopped),
            ("removing", -1, ModuleStatus::Failed),
            ("dead", -2, ModuleStatus::Failed),
            ("exited", -42, ModuleStatus::Failed),
            ("running", 0, ModuleStatus::Running),
        ]
    }

    #[test]
    fn module_status() {
        let inputs = get_inputs();

        for &(docker_status, exit_code, ref module_status) in inputs.iter() {
            let docker_module = DockerModule::new(
                create_api_client(
                    InlineResponse200::new().with_state(
                        InlineResponse200State::new()
                            .with_status(docker_status.to_string())
                            .with_exit_code(exit_code),
                    ),
                ),
                "mod1",
                DockerConfig::new("ubuntu", ContainerCreateBody::new(), None).unwrap(),
            ).unwrap();

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
                    ).with_id("mod1".to_string())
                    .with_exec_i_ds(vec!["id1".to_string(), "id2".to_string()]),
            ),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new(), None).unwrap(),
        ).unwrap();

        let runtime_state = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(docker_module.runtime_state())
            .unwrap();
        assert_eq!(ModuleStatus::Running, *runtime_state.status());
        assert_eq!(10, *runtime_state.exit_code().unwrap());
        assert_eq!(&"running", &runtime_state.status_description().unwrap());
        assert_eq!(started_at, runtime_state.started_at().unwrap().to_rfc3339());
        assert_eq!(
            finished_at,
            runtime_state.finished_at().unwrap().to_rfc3339()
        );
        assert_eq!(Pid::Value(1234), *runtime_state.pid());
    }

    #[test]
    fn module_runtime_state_failed_from_dead() {
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
                    ).with_id("mod1".to_string()),
            ),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new(), None).unwrap(),
        ).unwrap();

        let runtime_state = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(docker_module.runtime_state())
            .unwrap();
        assert_eq!(ModuleStatus::Failed, *runtime_state.status());
        assert_eq!(10, *runtime_state.exit_code().unwrap());
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
                            .with_started_at(started_at.clone())
                            .with_finished_at(finished_at.clone()),
                    ).with_id("mod1".to_string()),
            ),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new(), None).unwrap(),
        ).unwrap();

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
                            .with_started_at(started_at.clone())
                            .with_finished_at(finished_at.clone()),
                    ).with_id("mod1".to_string()),
            ),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new(), None).unwrap(),
        ).unwrap();

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
                            .with_started_at(started_at.clone())
                            .with_finished_at(finished_at.clone()),
                    ).with_id("mod1".to_string()),
            ),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new(), None).unwrap(),
        ).unwrap();

        let runtime_state = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(docker_module.runtime_state())
            .unwrap();
        assert_eq!(None, runtime_state.started_at());
        assert_eq!(None, runtime_state.finished_at());
    }
}
