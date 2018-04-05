// Copyright (c) Microsoft. All rights reserved.

use std::str::FromStr;

use chrono::prelude::*;
use futures::Future;
use hyper::client::Connect;

use client::DockerClient;
use config::DockerConfig;
use edgelet_core::{Module, ModuleRuntimeState, ModuleStatus};
use error::{Error, ErrorKind, Result};

pub const MODULE_TYPE: &str = "docker";

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

impl<C: Connect> Module for DockerModule<C> {
    type Config = DockerConfig;
    type Error = Error;
    type StatusFuture = Box<Future<Item = ModuleStatus, Error = Self::Error>>;
    type RuntimeStateFuture = Box<Future<Item = ModuleRuntimeState, Error = Self::Error>>;

    fn name(&self) -> &str {
        &self.name
    }

    fn type_(&self) -> &str {
        MODULE_TYPE
    }

    fn status(&self) -> Self::StatusFuture {
        Box::new(
            self.client
                .container_api()
                .container_inspect(&self.name, false)
                .map(|resp| {
                    resp.state()
                        .and_then(|state| state.status())
                        .and_then(|status| ModuleStatus::from_str(status).ok())
                        .unwrap_or_else(|| ModuleStatus::Unknown)
                })
                .map_err(|err| Error::from(ErrorKind::Docker(err))),
        )
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
                            ModuleRuntimeState::default()
                                .with_exit_code(state.exit_code().cloned())
                                .with_status_description(state.status().cloned())
                                .with_started_at(
                                    state
                                        .started_at()
                                        .and_then(|started_at| DateTime::from_str(started_at).ok()),
                                )
                                .with_finished_at(
                                    state.finished_at().and_then(|finished_at| {
                                        DateTime::from_str(finished_at).ok()
                                    }),
                                )
                                .with_image_id(resp.id().cloned())
                        })
                        .unwrap_or_else(ModuleRuntimeState::default)
                })
                .map_err(|err| Error::from(ErrorKind::Docker(err))),
        )
    }
}

#[cfg(test)]
mod tests {
    use std::string::ToString;

    use chrono::prelude::*;
    use hyper::Client;
    use serde::Serialize;
    use time::Duration;
    use tokio_core::reactor::Core;

    use docker_rs::apis::client::APIClient;
    use docker_rs::apis::configuration::Configuration;
    use docker_rs::models::{ContainerCreateBody, InlineResponse200, InlineResponse200State};
    use edgelet_core::{Module, ModuleStatus};
    use edgelet_test_utils::JsonConnector;

    use client::DockerClient;
    use config::DockerConfig;
    use module::DockerModule;

    fn create_api_client<T: 'static + Serialize>(
        core: &Core,
        body: T,
    ) -> DockerClient<JsonConnector<T>> {
        let client = Client::configure()
            .connector(JsonConnector::new(body))
            .build(&core.handle());

        let mut config = Configuration::new(client);
        config.base_path = "http://localhost/".to_string();
        config.uri_composer =
            Box::new(|base_path, path| Ok(format!("{}{}", base_path, path).parse().unwrap()));

        DockerClient::new(APIClient::new(config))
    }

    #[test]
    fn new_instance() {
        let core = Core::new().unwrap();
        let docker_module = DockerModule::new(
            create_api_client(&core, "boo"),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new()).unwrap(),
        ).unwrap();
        assert_eq!("mod1", docker_module.name());
        assert_eq!("docker", docker_module.type_());
        assert_eq!("ubuntu", docker_module.config().image());
    }

    #[test]
    #[should_panic]
    fn empty_name_fails() {
        let core = Core::new().unwrap();
        let _docker_module = DockerModule::new(
            create_api_client(&core, "boo"),
            "",
            DockerConfig::new("ubuntu", ContainerCreateBody::new()).unwrap(),
        ).unwrap();
    }

    #[test]
    #[should_panic]
    fn white_space_name_fails() {
        let core = Core::new().unwrap();
        let _docker_module = DockerModule::new(
            create_api_client(&core, "boo"),
            "     ",
            DockerConfig::new("ubuntu", ContainerCreateBody::new()).unwrap(),
        ).unwrap();
    }

    fn verify_module_status(core: &mut Core, status: &ModuleStatus) {
        let docker_module = DockerModule::new(
            create_api_client(
                &core,
                InlineResponse200::new()
                    .with_state(InlineResponse200State::new().with_status(status.to_string())),
            ),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new()).unwrap(),
        ).unwrap();

        let actual_status = core.run(docker_module.status()).unwrap();
        assert_eq!(*status, actual_status);
    }

    #[test]
    fn module_status() {
        let inputs = [
            ModuleStatus::Unknown,
            ModuleStatus::Created,
            ModuleStatus::Paused,
            ModuleStatus::Restarting,
            ModuleStatus::Removing,
            ModuleStatus::Dead,
            ModuleStatus::Exited,
            ModuleStatus::Running,
        ];
        let mut core = Core::new().unwrap();

        for status in inputs.iter() {
            verify_module_status(&mut core, &status);
        }
    }

    #[test]
    fn module_runtime_state() {
        let started_at = Utc::now().to_rfc3339();
        let finished_at = (Utc::now() + Duration::hours(1)).to_rfc3339();
        let mut core = Core::new().unwrap();
        let docker_module = DockerModule::new(
            create_api_client(
                &core,
                InlineResponse200::new()
                    .with_state(
                        InlineResponse200State::new()
                            .with_exit_code(10)
                            .with_status("running".to_string())
                            .with_started_at(started_at.clone())
                            .with_finished_at(finished_at.clone()),
                    )
                    .with_id("mod1".to_string()),
            ),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new()).unwrap(),
        ).unwrap();

        let runtime_state = core.run(docker_module.runtime_state()).unwrap();
        assert_eq!(10, *runtime_state.exit_code().unwrap());
        assert_eq!(&"running", &runtime_state.status_description().unwrap());
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
        let mut core = Core::new().unwrap();
        let docker_module = DockerModule::new(
            create_api_client(
                &core,
                InlineResponse200::new()
                    .with_state(
                        InlineResponse200State::new()
                            .with_exit_code(10)
                            .with_status("running".to_string())
                            .with_started_at(started_at.clone())
                            .with_finished_at(finished_at.clone()),
                    )
                    .with_id("mod1".to_string()),
            ),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new()).unwrap(),
        ).unwrap();

        let runtime_state = core.run(docker_module.runtime_state()).unwrap();
        assert_eq!(None, runtime_state.started_at());
    }

    #[test]
    fn module_runtime_state_with_bad_finished_at() {
        let started_at = Utc::now().to_rfc3339();
        let finished_at = "nope, not a date".to_string();
        let mut core = Core::new().unwrap();
        let docker_module = DockerModule::new(
            create_api_client(
                &core,
                InlineResponse200::new()
                    .with_state(
                        InlineResponse200State::new()
                            .with_exit_code(10)
                            .with_status("running".to_string())
                            .with_started_at(started_at.clone())
                            .with_finished_at(finished_at.clone()),
                    )
                    .with_id("mod1".to_string()),
            ),
            "mod1",
            DockerConfig::new("ubuntu", ContainerCreateBody::new()).unwrap(),
        ).unwrap();

        let runtime_state = core.run(docker_module.runtime_state()).unwrap();
        assert_eq!(None, runtime_state.finished_at());
    }
}
