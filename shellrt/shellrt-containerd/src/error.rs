use std::fmt;
use std::fmt::Display;

pub use failure::{Backtrace, Context, Fail, ResultExt};
use log::*;

use shellrt_api::v0::{Error as ApiError, ErrorCode as ApiErrorCode};

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Incompatible API version")]
    IncompatibleVersion,

    #[fail(display = "Could not parse request")]
    InvalidRequest,

    #[fail(display = "Could not connect to containerd")]
    GrpcConnect,

    #[fail(display = "Unexpected error while communicating with containerd")]
    GrpcUnexpectedErr,

    #[fail(display = "Malformed image reference")]
    MalformedReference,

    #[fail(display = "Malformed credentials")]
    MalformedCredentials,

    #[fail(display = "Registry returned a malformed image manifest")]
    MalformedManifest,

    #[fail(display = "Could not parse Create request config")]
    MalformedCreateConfig,

    #[fail(display = "Missing \"k8s.gcr.io/pause:3.1\" image")]
    MissingPauseImage,

    #[fail(display = "Specified module does not exist")]
    ModuleDoesNotExist,

    #[fail(display = "Could not open module log file")]
    OpenLogFile,

    // TODO: this error is too broad. it might be better to map several of the more "informative"
    // containrs errors to specific ErrorKinds / ErrorCodes.
    #[fail(display = "Error while communicating with registry")]
    RegistryError,
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
    pub fn new(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }

    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
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

impl Into<ApiError> for Error {
    fn into(self: Error) -> ApiError {
        use ErrorKind::*;

        ApiError {
            code: match self.kind() {
                IncompatibleVersion => ApiErrorCode::IncompatibleVersion,
                InvalidRequest => ApiErrorCode::InvalidRequest,
                // XXX: assign specific error codes to all ErrorKind variants
                _ => ApiErrorCode::Other(999),
            },
            message: {
                error!("{:?}", self.to_string());
                self.to_string()
            },
            // TODO: make the details nicer
            detail: {
                let err: failure::Error = self.into();
                Some(serde_json::json!({
                    "caused_by": err.iter_causes().map(ToString::to_string).collect::<Vec<_>>()
                }))
            },
        }
    }
}
