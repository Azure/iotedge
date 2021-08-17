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

// #[cfg(test)]
// mod tests {
//     use super::parse_top_response;

//     use std::string::ToString;

//     use bollard::service::ContainerTopResponse;

//     use docker::models::ContainerCreateBody;

//     use edgelet_core::Module;
//     use edgelet_settings::DockerConfig;

//     use crate::client::DockerClient;
//     use crate::module::DockerModule;

//     async fn get_client() -> DockerClient {
//         DockerClient::new(&url::Url::parse("unix:///var/run/docker.sock").unwrap())
//             .await
//             .unwrap()
//     }

//     #[tokio::test]
//     async fn new_instance() {
//         let docker_module = DockerModule::new(
//             get_client().await,
//             "mod1".to_string(),
//             DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
//                 .unwrap(),
//         )
//         .unwrap();

//         assert_eq!("mod1", docker_module.name());
//         assert_eq!("docker", docker_module.type_());
//         assert_eq!("ubuntu", docker_module.config().image());
//     }

//     #[tokio::test]
//     async fn empty_name_fails() {
//         let _ = DockerModule::new(
//             get_client().await,
//             "".to_string(),
//             DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
//                 .unwrap(),
//         )
//         .unwrap_err();
//     }

//     #[tokio::test]
//     async fn white_space_name_fails() {
//         let _ = DockerModule::new(
//             get_client().await,
//             "     ".to_string(),
//             DockerConfig::new("ubuntu".to_string(), ContainerCreateBody::new(), None, None)
//                 .unwrap(),
//         )
//         .unwrap_err();
//     }

//     // fn get_inputs() -> Vec<(&'static str, i64, ModuleStatus)> {
//     //     vec![
//     //         ("created", 0, ModuleStatus::Stopped),
//     //         ("paused", 0, ModuleStatus::Stopped),
//     //         ("restarting", 0, ModuleStatus::Stopped),
//     //         ("removing", 0, ModuleStatus::Stopped),
//     //         ("dead", 0, ModuleStatus::Stopped),
//     //         ("exited", 0, ModuleStatus::Stopped),
//     //         ("removing", -1, ModuleStatus::Failed),
//     //         ("dead", -2, ModuleStatus::Failed),
//     //         ("exited", -42, ModuleStatus::Failed),
//     //         ("running", 0, ModuleStatus::Running),
//     //     ]
//     // }

//     #[test]
//     fn parse_top_response_returns_pid_array() {
//         let response = ContainerTopResponse {
//             titles: Some(vec!["PID".to_string()]),
//             processes: Some(vec![vec!["123".to_string()]]),
//         };

//         let pids = parse_top_response("test", &response);

//         assert_eq!(vec![123], pids.unwrap());
//     }

//     #[test]
//     fn parse_top_response_returns_error_when_titles_is_missing() {
//         let response = ContainerTopResponse {
//             titles: None,
//             processes: Some(vec![vec!["123".to_string()]]),
//         };

//         let pids = parse_top_response("test", &response);

//         assert!(format!("{}", pids.unwrap_err()).contains("Expected 'Title' field"));
//     }

//     #[test]
//     fn parse_top_response_returns_error_when_pid_title_is_missing() {
//         let response = ContainerTopResponse {
//             titles: Some(vec![]),
//             processes: Some(vec![vec!["123".to_string()]]),
//         };

//         let pids = parse_top_response("test", &response);

//         assert_contains(&format!("{}", pids.unwrap_err()), "Expected Title 'PID'");
//     }

//     #[test]
//     fn parse_top_response_returns_error_when_processes_is_missing() {
//         let response = ContainerTopResponse {
//             titles: Some(vec!["PID".to_string()]),
//             processes: None,
//         };

//         let pids = parse_top_response("test", &response);

//         assert_contains(
//             &format!("{}", pids.unwrap_err()),
//             "Expected 'Processes' field",
//         );
//     }

//     #[test]
//     fn parse_top_response_returns_error_when_process_pid_is_missing() {
//         let response = ContainerTopResponse {
//             titles: Some(vec!["Command".to_string(), "PID".to_string()]),
//             processes: Some(vec![vec!["sh".to_string()]]),
//         };

//         let pids = parse_top_response("test", &response);

//         assert_contains(&format!("{}", pids.unwrap_err()), "PID index empty");
//     }

//     #[test]
//     fn parse_top_response_returns_error_when_process_pid_is_not_i64() {
//         let response = ContainerTopResponse {
//             titles: Some(vec!["PID".to_string()]),
//             processes: Some(vec![vec!["xyz".to_string()]]),
//         };

//         let pids = parse_top_response("test", &response);

//         assert_contains(
//             &format!("{}", pids.unwrap_err()),
//             "invalid digit found in string",
//         );
//     }

//     fn assert_contains(value: &str, expected_contents: &str) {
//         assert!(
//             value.contains(expected_contents),
//             format!(r#"Expected "{}" to contain "{}""#, value, expected_contents)
//         );
//     }
// }
