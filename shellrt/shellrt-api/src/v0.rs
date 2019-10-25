//! Types and constants used by the shellrt spec v0.
use serde::de::DeserializeOwned;
use serde::{Deserialize, Serialize};

mod error;
mod pull;
mod remove;
mod rtversion;

pub use error::{Error, ErrorCode};

pub const VERSION: &str = "0.1.0";

/// Ergonomic types for creating input payloads and parsing output payloads.
pub mod client {
    use super::*;
    input_newtype!(VERSION);
    output_newtype!(VERSION);
}

/// Ergonomic types parsing varying input payloads and creating varying output
/// payloads.
pub mod plugin {
    use super::*;

    /// Input JSON sent to plugin via stdin.
    pub type Input = super::client::Input<Request>;

    /// Output JSON sent from plugin via stdout.
    pub type Output = super::client::Output<Response>;

    /// Enumeration over all possible action request payloads.
    #[derive(Debug, Serialize, Deserialize)]
    #[serde(untagged)]
    pub enum Request {
        Pull(request::Pull),
        Remove(request::Remove),
        RuntimeVersion(request::RuntimeVersion),
    }

    /// Enumeration over all possible action response payloads.
    #[derive(Debug, Serialize, Deserialize)]
    #[serde(untagged)]
    pub enum Response {
        Pull(response::Pull),
        Remove(response::Remove),
        RuntimeVersion(response::RuntimeVersion),
    }

    impl ReqMarker for Request {}
    impl ResMarker for Response {}
}

/// Request types
pub mod request {
    pub use super::pull::PullRequest as Pull;
    pub use super::remove::RemoveRequest as Remove;
    pub use super::rtversion::RuntimeVersionRequest as RuntimeVersion;
}

/// Response types
pub mod response {
    pub use super::pull::PullResponse as Pull;
    pub use super::remove::RemoveResponse as Remove;
    pub use super::rtversion::RuntimeVersionResponse as RuntimeVersion;
}

/// Marker trait to prevent instantiating invalid input payloads.
/// This trait is Sealed, and cannot be implemented by users.
pub trait ReqMarker: private::Sealed + Serialize + DeserializeOwned {}
/// Marker trait to prevent instantiating invalid output payloads.
/// This trait is Sealed, and cannot be implemented by users.
pub trait ResMarker: private::Sealed + Serialize + DeserializeOwned {}

mod private {
    use super::*;
    pub trait Sealed {}

    impl Sealed for plugin::Request {}
    impl Sealed for plugin::Response {}

    impl Sealed for request::Pull {}
    impl Sealed for request::Remove {}
    impl Sealed for request::RuntimeVersion {}

    impl Sealed for response::Pull {}
    impl Sealed for response::Remove {}
    impl Sealed for response::RuntimeVersion {}
}
