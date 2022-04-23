// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;

use anyhow::Context;
use serde::de::DeserializeOwned;
use serde::Serialize;

use edgelet_core::{
    ImagePullPolicy, Module, ModuleRuntime, ModuleSpec as CoreModuleSpec, ModuleStatus,
};
use management::models::{Config, EnvVar, ModuleDetails, ModuleSpec, RuntimeStatus, Status};

use crate::error::Error;

mod create;
mod delete;
mod get;
mod list;
mod logs;
mod prepare_update;
mod restart;
mod start;
mod stop;
mod update;

pub use self::create::CreateModule;
pub use self::delete::DeleteModule;
pub use self::get::GetModule;
pub use self::list::ListModules;
pub use self::logs::ModuleLogs;
pub use self::prepare_update::PrepareUpdateModule;
pub use self::restart::RestartModule;
pub use self::start::StartModule;
pub use self::stop::StopModule;
pub use self::update::UpdateModule;

fn spec_to_core<M>(
    spec: &ModuleSpec,
) -> anyhow::Result<CoreModuleSpec<<M::Module as Module>::Config>>
where
    M: 'static + ModuleRuntime,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
{
    let name = spec.name().to_string();
    let type_ = spec.type_().to_string();
    let env = spec.config().env().map_or_else(BTreeMap::new, |vars| {
        vars.iter()
            .map(|var| (var.key().clone(), var.value().clone()))
            .collect()
    });

    let config = serde_json::from_value(spec.config().settings().clone())
        .context(Error::MalformedRequestBody)?;

    let image_pull_policy = spec
        .image_pull_policy()
        .map_or(Ok(ImagePullPolicy::default()), str::parse)
        .context(Error::MalformedRequestBody)?;

    let module_spec = CoreModuleSpec::new(name, type_, config, env, image_pull_policy)
        .context(Error::MalformedRequestBody)?;

    Ok(module_spec)
}

fn spec_to_details(spec: &ModuleSpec, module_status: ModuleStatus) -> ModuleDetails {
    let id = spec.name().clone();
    let name = spec.name().clone();
    let type_ = spec.type_().clone();

    let env = spec.config().env().map(|e| {
        e.iter()
            .map(|ev| EnvVar::new(ev.key().clone(), ev.value().clone()))
            .collect()
    });
    let mut config = Config::new(spec.config().settings().clone());
    if let Some(e) = env {
        config.set_env(e);
    }

    let runtime_status = RuntimeStatus::new(module_status.to_string());
    let status = Status::new(runtime_status);
    ModuleDetails::new(id, name, type_, config, status)
}

#[cfg(test)]
pub mod tests {
    use futures::{Future, Stream};
    use hyper::{Body, Response, StatusCode};

    use edgelet_core::RuntimeOperation;
    use edgelet_docker::Error as DockerError;
    use management::models::ErrorResponse;

    use crate::error::Error;
    use crate::IntoResponse;

    #[test]
    fn not_found() {
        // arrange
        let error = anyhow::anyhow!(DockerError::NotFound("No such container: m1".to_string()))
            .context(DockerError::RuntimeOperation(
                RuntimeOperation::StartModule("m1".to_string()),
            ))
            .context(Error::RuntimeOperation(RuntimeOperation::StartModule(
                "m1".to_string(),
            )));

        // act
        let response: Response<Body> = error.into_response();

        // assert
        assert_eq!(StatusCode::NOT_FOUND, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Could not start module m1\n\nCaused by:\n    0: Could not start module m1\n    1: No such container: m1",
                    error.message()
                );
                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn conflict() {
        // arrange
        let error = anyhow::anyhow!(DockerError::Conflict)
            .context(DockerError::RuntimeOperation(
                RuntimeOperation::StartModule("m1".to_string()),
            ))
            .context(Error::RuntimeOperation(RuntimeOperation::StartModule(
                "m1".to_string(),
            )));

        // act
        let response: Response<Body> = error.into_response();

        // assert
        assert_eq!(StatusCode::CONFLICT, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Could not start module m1\n\nCaused by:\n    0: Could not start module m1\n    1: Conflict with current operation",
                    error.message()
                );
                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn not_modified() {
        // arrange
        let error = anyhow::anyhow!(DockerError::NotModified)
            .context(DockerError::RuntimeOperation(RuntimeOperation::StopModule(
                "m1".to_string(),
            )))
            .context(Error::RuntimeOperation(RuntimeOperation::StopModule(
                "m1".to_string(),
            )));

        // act
        let response: Response<Body> = error.into_response();

        // assert
        assert_eq!(StatusCode::NOT_MODIFIED, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                assert!(b.into_bytes().is_empty());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn internal_server() {
        // arrange
        let error = anyhow::anyhow!(DockerError::Docker(anyhow::anyhow!("DOCKER"))).context(
            Error::RuntimeOperation(RuntimeOperation::StartModule("m1".to_string())),
        );

        // act
        let response: Response<Body> = error.into_response();

        // assert
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Could not start module m1\n\nCaused by:\n    0: Container runtime error\n    1: DOCKER",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn formatted_docker_runtime() {
        // arrange
        let error = anyhow::anyhow!(DockerError::FormattedDockerRuntime(
            "manifest for image:latest not found".to_string(),
        ))
        .context(Error::RuntimeOperation(RuntimeOperation::StartModule(
            "m1".to_string(),
        )));

        // act
        let response: Response<Body> = error.into_response();

        // assert
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Could not start module m1\n\nCaused by:\n    manifest for image:latest not found",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
