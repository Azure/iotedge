// Copyright (c) Microsoft. All rights reserved.

use hyper::StatusCode;

use docker::apis::{ApiError as DockerApiError, Error as DockerError};
use edgelet_core::{
    ModuleOperation, ModuleRuntimeErrorReason, RegistryOperation, RuntimeOperation,
};

fn get_message(
    error: DockerApiError<serde_json::Value>,
) -> ::std::result::Result<String, DockerApiError<serde_json::Value>> {
    let DockerApiError { code, content } = error;

    match content {
        Some(serde_json::Value::Object(props)) => {
            if let Some(serde_json::Value::String(message)) = props.get("message") {
                return Ok(message.clone());
            }

            Err(DockerApiError {
                code,
                content: Some(serde_json::Value::Object(props)),
            })
        }
        _ => Err(DockerApiError { code, content }),
    }
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("Could not clone create options")]
    CloneCreateOptions,

    #[error("Conflict with current operation")]
    Conflict,

    #[error("Container runtime error")]
    Docker(#[source] anyhow::Error),

    #[error("{0}")]
    FormattedDockerRuntime(String),

    #[error("Could not initialize module runtime")]
    Initialization,

    #[error("Could not initialize Notary configuration: {0}")]
    InitializeNotary(String),

    #[error("Invalid docker image {0}")]
    InvalidImage(String),

    #[error("Invalid module name {0}")]
    InvalidModuleName(String),

    #[error("Invalid module type {0}")]
    InvalidModuleType(String),

    #[error("Invalid socket URI: {0}")]
    InvalidSocketUri(String),

    #[error("Invalid home directory")]
    InvalidHomeDirPath,

    #[error("{0}")]
    LaunchNotary(&'static str),

    #[error("{0}")]
    ModuleOperation(ModuleOperation),

    #[error("notary digest mismatch with the manifest")]
    NotaryDigestMismatch,

    #[error("notary root CA read error")]
    NotaryRootCARead,

    #[error("{0}")]
    NotFound(String),

    #[error("Target of operation already in this state")]
    NotModified,

    #[error("{0}")]
    RegistryOperation(RegistryOperation),

    #[error("{0}")]
    RuntimeOperation(RuntimeOperation),
}

impl From<DockerError<serde_json::Value>> for Error {
    fn from(err: DockerError<serde_json::Value>) -> Self {
        match err {
            DockerError::Hyper(error) => Error::Docker(error.into()),
            DockerError::Serde(error) => Error::Docker(error.into()),
            DockerError::Api(error) => match error.code {
                StatusCode::NOT_FOUND => match get_message(error) {
                    Ok(message) => Error::NotFound(message),
                    Err(e) => Error::Docker(anyhow::anyhow!("{:?}", e)),
                },
                StatusCode::CONFLICT => Error::Conflict,
                StatusCode::NOT_MODIFIED => Error::NotModified,
                _ => match get_message(error) {
                    Ok(message) => Error::FormattedDockerRuntime(message),
                    Err(e) => Error::Docker(anyhow::anyhow!("{:?}", e)),
                },
            },
        }
    }
}


impl<'a> From<&'a Error> for ModuleRuntimeErrorReason {
    fn from(err: &'a Error) -> Self {
        let mut err: &dyn std::error::Error = err;
        while let Some(cause) = err.source() {
            err = cause;
        }

        match err.downcast_ref() {
            Some(Error::NotFound(_)) => ModuleRuntimeErrorReason::NotFound,
            _ => ModuleRuntimeErrorReason::Other,
        }
    }
}
