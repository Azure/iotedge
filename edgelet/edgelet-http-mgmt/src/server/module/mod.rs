// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;

use failure::Fail;
use serde::de::DeserializeOwned;
use serde::Serialize;
use serde_json;

use edgelet_core::{
    ImagePullPolicy, Module, ModuleRuntime, ModuleSpec as CoreModuleSpec, ModuleStatus,
};
use management::models::*;

use crate::error::{Error, ErrorKind};

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
    context: ErrorKind,
) -> Result<CoreModuleSpec<<M::Module as Module>::Config>, Error>
where
    M: 'static + ModuleRuntime,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
{
    let name = spec.name().to_string();
    let type_ = spec.type_().to_string();
    let env = spec.config().env().map_or_else(HashMap::new, |vars| {
        vars.iter()
            .map(|var| (var.key().clone(), var.value().clone()))
            .collect()
    });

    let config = match serde_json::from_value(spec.config().settings().clone()) {
        Ok(config) => config,
        Err(err) => return Err(Error::from(err.context(context))),
    };

    let image_pull_policy = match spec
        .image_pull_policy()
        .map_or(Ok(ImagePullPolicy::default()), str::parse)
    {
        Ok(image_pull_policy) => image_pull_policy,
        Err(err) => return Err(Error::from(err.context(context))),
    };

    let module_spec = match CoreModuleSpec::new(name, type_, config, env, image_pull_policy) {
        Ok(module_spec) => module_spec,
        Err(err) => return Err(Error::from(err.context(context))),
    };

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
    use failure::Fail;
    use futures::{Future, Stream};
    use hyper::{Body, Response, StatusCode};
    use serde_json;

    use edgelet_core::RuntimeOperation;
    use edgelet_docker::{Error as DockerError, ErrorKind as DockerErrorKind};
    use management::models::ErrorResponse;

    use crate::error::{Error as MgmtError, ErrorKind};
    use crate::IntoResponse;

    #[derive(Clone, Copy, Debug, Fail)]
    pub enum Error {
        #[fail(display = "General error")]
        General,
    }

    impl IntoResponse for Error {
        fn into_response(self) -> Response<Body> {
            let body = serde_json::to_string(&ErrorResponse::new(self.to_string()))
                .expect("serialization of ErrorResponse failed.");
            Response::builder()
                .status(StatusCode::INTERNAL_SERVER_ERROR)
                .body(body.into())
                .unwrap()
        }
    }

    #[test]
    fn not_found() {
        // arrange
        let error = MgmtError::from(
            DockerError::from(
                DockerErrorKind::NotFound("No such container: m1".to_string()).context(
                    DockerErrorKind::RuntimeOperation(RuntimeOperation::StartModule(
                        "m1".to_string(),
                    )),
                ),
            )
            .context(ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(
                "m1".to_string(),
            ))),
        );

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::NOT_FOUND, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Could not start module m1\n\tcaused by: Could not start module m1\n\tcaused by: No such container: m1",
                    error.message()
                );
                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn conflict() {
        // arrange
        let error = MgmtError::from(
            DockerError::from(DockerErrorKind::Conflict.context(
                DockerErrorKind::RuntimeOperation(RuntimeOperation::StartModule("m1".to_string())),
            ))
            .context(ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(
                "m1".to_string(),
            ))),
        );

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::CONFLICT, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Could not start module m1\n\tcaused by: Could not start module m1\n\tcaused by: Conflict with current operation",
                    error.message()
                );
                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn not_modified() {
        // arrange
        let error = MgmtError::from(
            DockerError::from(DockerErrorKind::NotModified.context(
                DockerErrorKind::RuntimeOperation(RuntimeOperation::StopModule("m1".to_string())),
            ))
            .context(ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(
                "m1".to_string(),
            ))),
        );

        // act
        let response = error.into_response();

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
        let error = MgmtError::from(DockerError::from(DockerErrorKind::Docker).context(
            ErrorKind::RuntimeOperation(RuntimeOperation::StartModule("m1".to_string())),
        ));

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Could not start module m1\n\tcaused by: Container runtime error",
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
        let error = MgmtError::from(
            DockerError::from(DockerErrorKind::FormattedDockerRuntime(
                "manifest for image:latest not found".to_string(),
            ))
            .context(ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(
                "m1".to_string(),
            ))),
        );

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Could not start module m1\n\tcaused by: manifest for image:latest not found",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
