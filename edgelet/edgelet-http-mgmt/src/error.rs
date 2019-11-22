// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};

use edgelet_core::{IdentityOperation, ModuleOperation, RuntimeOperation};
use edgelet_docker::ErrorKind as DockerErrorKind;
use edgelet_iothub::Error as IoTHubError;
use failure::{Backtrace, Context, Fail};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Response, StatusCode};
use log::error;
use serde_json;

use management::apis::Error as MgmtError;
use management::models::ErrorResponse;

use crate::IntoResponse;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    // Note: This errorkind is always wrapped in another errorkind context
    #[fail(display = "Client error")]
    Client(MgmtError<serde_json::Value>),

    #[fail(display = "{}", _0)]
    IdentityOperation(IdentityOperation),

    #[fail(display = "Could not initialize module client")]
    InitializeModuleClient,

    #[fail(display = "Invalid API version {:?}", _0)]
    InvalidApiVersion(String),

    #[fail(display = "A request to Azure IoT Hub failed")]
    IotHub,

    #[fail(display = "Request body is malformed")]
    MalformedRequestBody,

    #[fail(display = "The request parameter `{}` is malformed", _0)]
    MalformedRequestParameter(&'static str),

    #[fail(display = "The request is missing required parameter `{}`", _0)]
    MissingRequiredParameter(&'static str),

    #[fail(display = "{}", _0)]
    ModuleOperation(ModuleOperation),

    #[fail(display = "State not modified")]
    NotModified,

    #[fail(display = "Could not prepare update for module {:?}", _0)]
    PrepareUpdateModule(String),

    #[fail(display = "Could not reprovision device")]
    ReprovisionDevice,

    #[fail(display = "{}", _0)]
    RuntimeOperation(RuntimeOperation),

    #[fail(display = "Could not start management service")]
    StartService,

    #[fail(display = "Could not update module {:?}", _0)]
    UpdateModule(String),
}

impl Fail for Error {
    fn cause(&self) -> Option<&dyn Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        Display::fmt(&self.inner, f)
    }
}

impl Error {
    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }

    pub fn from_mgmt_error(error: MgmtError<serde_json::Value>, context: ErrorKind) -> Self {
        match error {
            MgmtError::Hyper(h) => Error::from(h.context(context)),
            MgmtError::Serde(s) => Error::from(s.context(context)),
            MgmtError::Api(ref e) if e.code == StatusCode::NOT_MODIFIED => {
                Error::from(ErrorKind::NotModified)
            }
            MgmtError::Api(_) => Error::from(ErrorKind::Client(error).context(context)),
        }
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }
}

impl IntoResponse for Error {
    fn into_response(self) -> Response<Body> {
        let mut message = self.to_string();
        for cause in Fail::iter_causes(&self) {
            message.push_str(&format!("\n\tcaused by: {}", cause));
        }

        // Specialize status code based on the underlying docker runtime error, if any
        let status_code =
            if let Some(cause) = Fail::find_root_cause(&self).downcast_ref::<DockerErrorKind>() {
                match cause {
                    DockerErrorKind::NotFound(_) => StatusCode::NOT_FOUND,
                    DockerErrorKind::Conflict => StatusCode::CONFLICT,
                    DockerErrorKind::NotModified => StatusCode::NOT_MODIFIED,
                    _ => StatusCode::INTERNAL_SERVER_ERROR,
                }
            } else {
                match self.kind() {
                    ErrorKind::InvalidApiVersion(_)
                    | ErrorKind::MalformedRequestBody
                    | ErrorKind::MalformedRequestParameter(_)
                    | ErrorKind::MissingRequiredParameter(_) => StatusCode::BAD_REQUEST,
                    _ => {
                        error!("Internal server error: {}", message);
                        StatusCode::INTERNAL_SERVER_ERROR
                    }
                }
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

impl IntoResponse for IoTHubError {
    fn into_response(self) -> Response<Body> {
        Error::from(self.context(ErrorKind::IotHub)).into_response()
    }
}
