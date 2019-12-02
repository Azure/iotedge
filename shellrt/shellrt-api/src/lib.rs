#![deny(missing_docs)]

//! Types and constants used by the shellrt spec.

mod util;

/// Common methods across client/plugin Input/Output types.
macro_rules! impl_accessors {
    ($inner_name:ident, $inner_type:ty) => {
        /// shellrt API version
        pub fn version(&self) -> &str {
            &self.version
        }

        /// Get a reference to the message payload
        pub fn inner_ref(&self) -> &$inner_type {
            &self.$inner_name
        }

        /// Get a mutable reference to the message payload
        pub fn inner_mut(&mut self) -> &mut $inner_type {
            &mut self.$inner_name
        }

        /// Consumes self, returning the message payload
        pub fn into_inner(self) -> $inner_type {
            self.$inner_name
        }
    };
}

// Sure, this macro _could_ have used some boring syntax, but where's the fun in
// that? Instead, lets give it a bit of flair ✨
macro_rules! api_impl {
    (
            + $(-)*                                                                 +
            | version $version:literal                                              |
            + $(-)*         + $(-)*      ++ $(-)*         + $(-)*      + $(-)*      +
            | name          | json tag   || module        | request    | response   |
        $($(+ $(-)*         + $(-)*      ++ $(-)*         + $(-)*      + $(-)*      +)?
            | $name:ident | $tag:literal || $module:ident | $req:ident | $res:ident |)*
            + $(-)*         + $(-)*      ++ $(-)*         + $(-)*      + $(-)*      +
    ) => {
        use serde::de::DeserializeOwned;
        use serde::{Deserialize, Serialize};

        /// shellrt version string
        pub const VERSION: &str = $version;

        mod error;
        pub use error::{Error, ErrorCode};

        $(mod $module;)*

        /// Ergonomic types for sending requests to / receiving responses from plugins.
        pub mod client {
            use super::*;

            /// Input JSON sent to a plugin via stdin.
            #[derive(Debug, Serialize, Deserialize)]
            #[serde(bound(deserialize = "Request: DeserializeOwned"))]
            pub struct Input<Request: ReqMarker> {
                #[serde(rename = "_version")]
                version: String,
                #[serde(rename = "_type")]
                type_: String,
                #[serde(flatten)]
                request: Request,
            }

            impl<Request: ReqMarker> Input<Request> {
                /// Create a new Input message with the specified `request`.
                pub fn new(request: Request) -> Input<Request> {
                    Input {
                        version: VERSION.to_string(),
                        type_: request.payload_tag().to_string(),
                        request,
                    }
                }

                impl_accessors!(request, Request);
            }

            /// Output JSON sent from a plugin a via stdout.
            #[derive(Debug, Serialize, Deserialize)]
            #[serde(bound(deserialize = "Response: DeserializeOwned"))]
            pub struct Output<Response: ResMarker> {
                #[serde(rename = "_version")]
                version: String,
                #[serde(flatten)]
                #[serde(with = "crate::util::api_result")]
                response: Result<Response, Error>,
            }

            impl<Response: ResMarker> Output<Response> {
                /// Create a new Output message with the specified `response`.
                pub fn new(response: Result<Response, Error>) -> Output<Response> {
                    Output {
                        version: VERSION.to_string(),
                        response,
                    }
                }

                impl_accessors!(response, Result<Response, Error>);
            }
        }

        /// Ergonomic types for receiving requests from / sending responses to clients.
        pub mod plugin {
            use super::*;

            /// Enumeration over all possible action request payloads.
            #[derive(Debug, Serialize, Deserialize)]
            #[serde(tag = "_type")]
            pub enum Request {
                $(
                    #[serde(rename = $tag)]
                    #[allow(missing_docs)]
                    $name(request::$name),
                )*
            }

            /// Enumeration over all possible action response payloads.
            #[derive(Debug, Serialize, Deserialize)]
            #[serde(untagged)]
            pub enum Response {
                $(
                    #[allow(missing_docs)]
                    $name(response::$name),
                )*
            }

            /// Input JSON sent from a plugin via stdout.
            #[derive(Debug, Serialize, Deserialize)]
            pub struct Input {
                #[serde(rename = "_version")]
                version: String,
                #[serde(flatten)]
                request: Request,
            }

            impl Input {
                /// Create a new Input message with the specified `request`.
                pub fn new(request: Request) -> Input {
                    Input {
                        version: VERSION.to_string(),
                        request,
                    }
                }

                impl_accessors!(request, Request);
            }

            /// Output JSON sent from a plugin via stdout.
            #[derive(Debug, Serialize, Deserialize)]
            pub struct Output {
                #[serde(rename = "_version")]
                version: String,
                #[serde(flatten)]
                #[serde(with = "crate::util::api_result")]
                response: Result<Response, Error>,
            }

            impl Output {
                /// Create a new Output message with the specified `response`.
                pub fn new(response: Result<Response, Error>) -> Output {
                    Output {
                        version: VERSION.to_string(),
                        response,
                    }
                }

                impl_accessors!(response, Result<Response, Error>);
            }
        }

        /// Request types
        pub mod request {
            $(pub use super::$module::$req as $name;)*
        }

        /// Response types
        pub mod response {
            $(pub use super::$module::$res as $name;)*
        }

        mod private {
            use super::*;
            pub trait Sealed {}
            $(impl Sealed for request::$name {})*
            $(impl Sealed for response::$name {})*
        }

        /// A Marker trait implemented by all Requests.
        /// This trait is Sealed, and cannot be implemented by users.
        ///
        /// In addition, this trait associates each Request with it's corresponding
        /// Response (via ReqMarker::Response)
        pub trait ReqMarker: private::Sealed + Serialize + DeserializeOwned + std::fmt::Debug {
            /// The Request's associated Response
            type Response: ResMarker;

            /// Returns the Request's api "type" tag
            fn payload_tag(&self) -> &'static str;
        }
        /// A Marker trait implemented by all Responses.
        /// This trait is Sealed, and cannot be implemented by users.
        ///
        /// In addition, this trait associates each Response with it's corresponding
        /// Request (via ReqMarker::Request)
        pub trait ResMarker: private::Sealed + Serialize + DeserializeOwned + std::fmt::Debug {
            /// Returns the Response's api "type" tag
            fn payload_tag(&self) -> &'static str;
            /// The Response's associated Request
            type Request: ReqMarker;
        }

        $(
            impl ReqMarker for request::$name {
                type Response = response::$name;
                fn payload_tag(&self) -> &'static str {
                    $tag
                }
            }
            impl ResMarker for response::$name {
                type Request = request::$name;
                fn payload_tag(&self) -> &'static str {
                    $tag
                }
            }
        )*
    }
}

/// shellrt api v0
pub mod v0 {
    pub use status::ModuleStatus;

    api_impl! {
        +------------------------------------------------------------------------------------+
        | version "0.1.0"                                                                    |
        +------------+---------------++-------------+-------------------+--------------------+
        | name       | json tag      || module      | request           | response           |
        +------------+---------------++-------------+-------------------+--------------------+
        | ImgPull    | "img_pull"    || img_pull    | ImgPullRequest    | ImgPullResponse    |
        | ImgRemove  | "img_remove"  || img_remove  | ImgRemoveRequest  | ImgRemoveResponse  |
        +------------+---------------++-------------+-------------------+--------------------+
        | Create     | "create"      || create      | CreateRequest     | CreateResponse     |
        | Remove     | "remove"      || remove      | RemoveRequest     | RemoveResponse     |
        | Restart    | "restart"     || restart     | RestartRequest    | RestartResponse    |
        | Start      | "start"       || start       | StartRequest      | StartResponse      |
        | Stop       | "stop"        || stop        | StopRequest       | StopResponse       |
        +------------+---------------++-------------+-------------------+--------------------+
        | List       | "list"        || list        | ListRequest       | ListResponse       |
     // | Logs       | "logs"        || logs        | LogsRequest       | LogsResponse       |
        | Status     | "status"      || status      | StatusRequest     | StatusResponse     |
     // | SystemInfo | "system_info" || system_info | SystemInfoRequest | SystemInfoResponse |
     // | Top        | "top"         || top         | TopRequest        | TopResponse        |
        +------------+---------------++-------------+-------------------+--------------------+
        | Version    | "version"     || version     | VersionRequest    | VersionResponse    |
        +------------+---------------++-------------+-------------------+--------------------+
    }
}
