//! Types and constants used by the shellrt spec.

use serde::de::DeserializeOwned;
use serde::{Deserialize, Serialize};

mod util;

#[derive(Debug, Serialize, Deserialize)]
struct Input<Request> {
    pub version: String,
    #[serde(flatten)]
    pub request: Request,
}

#[derive(Debug, Serialize, Deserialize)]
struct Output<Response, Error>
where
    Response: Serialize + DeserializeOwned,
    Error: Serialize + DeserializeOwned,
{
    pub version: String,
    #[serde(flatten)]
    #[serde(serialize_with = "crate::util::ser_api_result")]
    #[serde(deserialize_with = "crate::util::des_api_result")]
    pub response: Result<Response, Error>,
}

macro_rules! input_newtype {
    ($version:ident) => {
        /// Input JSON sent to plugin via stdin.
        #[derive(Debug, Serialize, Deserialize)]
        #[serde(bound(deserialize = "Request: DeserializeOwned"))]
        #[serde(transparent)]
        pub struct Input<Request: ReqMarker> {
            inner: crate::Input<Request>,
        }

        impl<Request: ReqMarker> Input<Request> {
            pub fn new(request: Request) -> Input<Request> {
                Input {
                    inner: crate::Input {
                        version: $version.to_string(),
                        request,
                    },
                }
            }

            pub fn version(&self) -> &str {
                &self.inner.version
            }

            pub fn get_ref(&self) -> &Request {
                &self.inner.request
            }

            pub fn get_mut(&mut self) -> &mut Request {
                &mut self.inner.request
            }

            pub fn into_inner(self) -> Request {
                self.inner.request
            }
        }
    };
}

macro_rules! output_newtype {
    ($version:ident) => {
        /// Output JSON sent from plugin via stdout.
        #[derive(Debug, Serialize, Deserialize)]
        #[serde(bound(deserialize = "Response: DeserializeOwned"))]
        #[serde(transparent)]
        pub struct Output<Response: ResMarker> {
            inner: crate::Output<Response, Error>,
        }

        impl<Response: ResMarker> Output<Response> {
            pub fn new(response: Result<Response, Error>) -> Output<Response> {
                Output {
                    inner: crate::Output {
                        version: $version.to_string(),
                        response,
                    },
                }
            }

            pub fn version(&self) -> &str {
                &self.inner.version
            }

            pub fn get_ref(&self) -> &Result<Response, Error> {
                &self.inner.response
            }

            pub fn get_mut(&mut self) -> &mut Result<Response, Error> {
                &mut self.inner.response
            }

            pub fn into_inner(self) -> Result<Response, Error> {
                self.inner.response
            }
        }
    };
}

// must be after macros
pub mod v0;
