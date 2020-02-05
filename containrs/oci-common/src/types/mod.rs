//! Types and constants used across various [OCI Specs](https://github.com/opencontainers/)

mod annotations;
mod env_var;

pub use annotations::Annotations;
pub use env_var::EnvVar;
