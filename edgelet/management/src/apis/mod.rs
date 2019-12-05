use hyper;
use serde;
use serde_json;

#[derive(Debug)]
pub enum Error<T> {
    Hyper(hyper::Error),
    Serde(serde_json::Error),
    Api(ApiError<T>),
}

#[derive(Debug)]
pub struct ApiError<T> {
    pub code: hyper::StatusCode,
    pub content: Option<T>,
}

impl<'de, T> From<(hyper::StatusCode, &'de [u8])> for Error<T>
where
    T: serde::Deserialize<'de>,
{
    fn from(e: (hyper::StatusCode, &'de [u8])) -> Self {
        if e.1.is_empty() {
            return Error::Api(ApiError {
                code: e.0,
                content: None,
            });
        }
        match serde_json::from_slice::<T>(e.1) {
            Ok(t) => Error::Api(ApiError {
                code: e.0,
                content: Some(t),
            }),
            Err(e) => e.into(),
        }
    }
}

impl<T> From<hyper::Error> for Error<T> {
    fn from(e: hyper::Error) -> Self {
        Error::Hyper(e)
    }
}

impl<T> From<serde_json::Error> for Error<T> {
    fn from(e: serde_json::Error) -> Self {
        Error::Serde(e)
    }
}

mod device_actions_api;
pub use self::device_actions_api::{DeviceActionsApi, DeviceActionsApiClient};
mod identity_api;
pub use self::identity_api::{IdentityApi, IdentityApiClient};
mod module_api;
pub use self::module_api::{ModuleApi, ModuleApiClient};
mod system_information_api;
pub use self::system_information_api::{SystemInformationApi, SystemInformationApiClient};

pub mod client;
pub mod configuration;
