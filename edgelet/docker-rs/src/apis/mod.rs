use hyper;
use serde;
use serde_json;

#[derive(Debug)]
pub enum Error<T> {
    Api(ApiError<T>),
    Hyper(hyper::Error),
    Serde(serde_json::Error),
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
        if e.1.len() == 0 {
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
            Err(e) => Error::from(e),
        }
    }
}

impl<'de> From<(hyper::StatusCode, serde_json::Value)> for Error<serde_json::Value> {
    fn from(e: (hyper::StatusCode, serde_json::Value)) -> Self {
        Error::Api(ApiError {
            code: e.0,
            content: Some(e.1),
        })
    }
}

impl<T> From<hyper::Error> for Error<T> {
    fn from(e: hyper::Error) -> Self {
        return Error::Hyper(e);
    }
}

impl<T> From<serde_json::Error> for Error<T> {
    fn from(e: serde_json::Error) -> Self {
        return Error::Serde(e);
    }
}

mod container_api;
pub use self::container_api::{ContainerApi, ContainerApiClient};
mod image_api;
pub use self::image_api::{ImageApi, ImageApiClient};
mod network_api;
pub use self::network_api::{NetworkApi, NetworkApiClient};
mod system_api;
pub use self::system_api::{SystemApi, SystemApiClient};
mod volume_api;
pub use self::volume_api::{VolumeApi, VolumeApiClient};

pub mod client;
pub mod configuration;
