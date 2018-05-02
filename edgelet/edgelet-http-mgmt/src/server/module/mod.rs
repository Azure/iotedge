// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;

use edgelet_core::{Module, ModuleRuntime, ModuleSpec as CoreModuleSpec, ModuleStatus};
use edgelet_docker::{Error as DockerError, ErrorKind as DockerErrorKind};
use failure::{Fail, ResultExt};
use futures::Future;
use hyper::StatusCode;
use hyper::header::{ContentLength, ContentType};
use hyper::server::Response;
use management::models::*;
use serde::Serialize;
use serde::de::DeserializeOwned;
use serde_json;

use error::{Error, ErrorKind};
use IntoResponse;

mod create;
mod delete;
mod get;
mod list;
mod restart;
mod start;
mod stop;
mod update;

pub use self::create::CreateModule;
pub use self::delete::DeleteModule;
pub use self::get::GetModule;
pub use self::list::ListModules;
pub use self::restart::RestartModule;
pub use self::start::StartModule;
pub use self::stop::StopModule;
pub use self::update::UpdateModule;

impl IntoResponse for DockerError {
    fn into_response(self) -> Response {
        let mut fail: &Fail = &self;
        let mut message = self.to_string();
        while let Some(cause) = fail.cause() {
            message.push_str(&format!("\n\tcaused by: {}", cause.to_string()));
            fail = cause;
        }

        let status_code = match *self.kind() {
            DockerErrorKind::NotFound => StatusCode::NotFound,
            DockerErrorKind::Conflict => StatusCode::Conflict,
            DockerErrorKind::NotModified => StatusCode::NotModified,
            _ => StatusCode::InternalServerError,
        };

        // Per the RFC, status code NotModified should not have a body
        let body = if status_code != StatusCode::NotModified {
            let b = serde_json::to_string(&ErrorResponse::new(message))
                .expect("serialization of ErrorResponse failed.");
            Some(b)
        } else {
            None
        };

        body.map(|b| {
            Response::new()
                .with_status(status_code)
                .with_header(ContentLength(b.len() as u64))
                .with_header(ContentType::json())
                .with_body(b)
        }).unwrap_or_else(|| Response::new().with_status(status_code))
    }
}

fn core_to_details<M>(module: M) -> Box<Future<Item = ModuleDetails, Error = Error>>
where
    M: 'static + Module,
    M::Config: Serialize,
{
    let details = module
        .runtime_state()
        .then(move |result| {
            result.context(ErrorKind::ModuleRuntime).and_then(|state| {
                serde_json::to_value(module.config())
                    .context(ErrorKind::Serde)
                    .map(|settings| {
                        let config = Config::new(settings).with_env(Vec::new());
                        let mut runtime_status = RuntimeStatus::new(state.status().to_string());
                        if let Some(description) = state.status_description() {
                            runtime_status.set_description(description.to_string());
                        }
                        let mut status = Status::new(runtime_status);
                        if let Some(started_at) = state.started_at() {
                            status.set_start_time(started_at.to_rfc3339());
                        }
                        if let Some(code) = state.exit_code() {
                            if let Some(finished_at) = state.finished_at() {
                                status.set_exit_status(ExitStatus::new(
                                    finished_at.to_rfc3339(),
                                    code.to_string(),
                                ));
                            }
                        }

                        ModuleDetails::new(
                            "id".to_string(),
                            module.name().to_string(),
                            module.type_().to_string(),
                            config,
                            status,
                        )
                    })
            })
        })
        .map_err(From::from);
    Box::new(details)
}

fn spec_to_core<M>(
    spec: &ModuleSpec,
) -> Result<CoreModuleSpec<<M::Module as Module>::Config>, Error>
where
    M: 'static + ModuleRuntime,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
{
    let name = spec.name();
    let type_ = spec.type_();
    let env = spec.config()
        .env()
        .map(|vars| {
            vars.into_iter()
                .map(|var| (var.key().clone(), var.value().clone()))
                .collect()
        })
        .unwrap_or_else(HashMap::new);
    let config = serde_json::from_value(spec.config().settings().clone())?;
    let module_spec = CoreModuleSpec::new(name, type_, config, env)?;
    Ok(module_spec)
}

fn spec_to_details(spec: &ModuleSpec) -> ModuleDetails {
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

    let runtime_status = RuntimeStatus::new(ModuleStatus::Stopped.to_string());
    let status = Status::new(runtime_status);
    ModuleDetails::new(id, name, type_, config, status)
}

#[cfg(test)]
mod tests {
    use edgelet_docker::{Error as DockerError, ErrorKind as DockerErrorKind};
    use futures::{Future, Stream};
    use hyper::StatusCode;
    use hyper::server::Response;
    use management::models::ErrorResponse;
    use serde_json;

    use IntoResponse;

    #[derive(Clone, Debug, Fail)]
    pub enum Error {
        #[fail(display = "General error")]
        General,
    }

    impl IntoResponse for Error {
        fn into_response(self) -> Response {
            let body = serde_json::to_string(&ErrorResponse::new(self.to_string()))
                .expect("serialization of ErrorResponse failed.");
            Response::new()
                .with_status(StatusCode::InternalServerError)
                .with_body(body)
        }
    }

    #[test]
    fn not_found() {
        // arrange
        let error = DockerError::from(DockerErrorKind::NotFound);

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::NotFound, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!("Container not found", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn conflict() {
        // arrange
        let error = DockerError::from(DockerErrorKind::Conflict);

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::Conflict, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!("Conflict with current operation", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn internal_server() {
        // arrange
        let error = DockerError::from(DockerErrorKind::UrlParse);

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::InternalServerError, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!("Invalid URL", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
