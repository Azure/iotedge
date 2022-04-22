// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{IdentityOperation, ModuleOperation, RuntimeOperation};
use edgelet_docker::Error as DockerError;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Response, StatusCode};
use identity_client::Error as IdentityClientError;
use log::error;

use management::apis::Error as MgmtError;
use management::models::ErrorResponse;

use crate::IntoResponse;

#[derive(Debug, thiserror::Error)]
pub enum Error {
    // Note: This errorkind is always wrapped in another errorkind context
    #[error("Client error")]
    Client(MgmtError<serde_json::Value>),

    #[error("{0}")]
    IdentityOperation(IdentityOperation),

    #[error("Could not initialize module client")]
    InitializeModuleClient,

    #[error("Invalid API version `{0}`")]
    InvalidApiVersion(String),

    #[error("Invalid Identity type")]
    InvalidIdentityType,

    #[error("A request to Azure IoT Hub failed")]
    IotHub,

    #[error("Request body is malformed")]
    MalformedRequestBody,

    #[error("The request parameter `{0}` is malformed")]
    MalformedRequestParameter(&'static str),

    #[error("The request is missing required parameter `{0}`")]
    MissingRequiredParameter(&'static str),

    #[error("{0}")]
    ModuleOperation(ModuleOperation),

    #[error("State not modified")]
    NotModified,

    #[error("Could not prepare update for module `{0}`")]
    PrepareUpdateModule(String),

    #[error("Could not reprovision device")]
    ReprovisionDevice,

    #[error("{0}")]
    RuntimeOperation(RuntimeOperation),

    #[error("Could not start management service")]
    StartService,

    #[error("Could not update module `{0}`")]
    UpdateModule(String),

    #[error("Could not collect support bundle")]
    SupportBundle,
}

impl From<IdentityClientError> for Error {
    fn from(value: IdentityClientError) -> Self {
        anyhow::anyhow!(value)
            .context(Error::IotHub)
            .downcast()
            .expect("should always have crate::Error")
    }
}

impl From<MgmtError<serde_json::Value>> for Error {
    fn from(value: MgmtError<serde_json::Value>) -> Self {
        match value {
            MgmtError::Api(ref e) if e.code == StatusCode::NOT_MODIFIED => {
                Error::NotModified
            }
            MgmtError::Hyper(_)
            | MgmtError::Serde(_)
            | MgmtError::Api(_) => Error::Client(value),
        }
    }
}

impl IntoResponse for anyhow::Error {
    fn into_response(self) -> Response<Body> {
        let message = format!("{:?}", self);

        // Specialize status code based on the underlying docker runtime error, if any
        let status_code = if let Some(docker_error) = self.root_cause().downcast_ref()
        {
            match docker_error {
                DockerError::NotFound(_) => StatusCode::NOT_FOUND,
                DockerError::Conflict => StatusCode::CONFLICT,
                DockerError::NotModified => StatusCode::NOT_MODIFIED,
                _ => StatusCode::INTERNAL_SERVER_ERROR,
            }
        } else if let Some(error) = self.downcast_ref() {
            match error {
                Error::InvalidApiVersion(_)
                | Error::InvalidIdentityType
                | Error::MalformedRequestBody
                | Error::MalformedRequestParameter(_)
                | Error::MissingRequiredParameter(_) => StatusCode::BAD_REQUEST,
                _ => {
                    error!("Internal server error: {}", message);
                    StatusCode::INTERNAL_SERVER_ERROR
                }
            }
        } else {
            StatusCode::INTERNAL_SERVER_ERROR
        };

        // Per the RFC, status code NotModified should not have a body
        let body = if status_code == StatusCode::NOT_MODIFIED {
            String::new()
        } else {
            serde_json::to_string(&ErrorResponse::new(message))
                .expect("serialization of ErrorResponse failed.")
        };

        let mut response = Response::builder();
        response
            .status(status_code)
            .header(CONTENT_LENGTH, body.len().to_string().as_str());
        if !body.is_empty() {
            response.header(CONTENT_TYPE, "application/json");
        }
        response
            .body(body.into())
            .expect("response builder failure")
    }
}
