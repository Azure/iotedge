//! Common digest package used across the OCI ecosystem
//!
//! Inspired by https://github.com/opencontainers/go-digest

mod algorithms;
mod digest;
mod error;
mod validator;

#[cfg(feature = "serde")]
mod serde_impl;

pub use algorithms::Algorithm;
pub use digest::Digest;
pub use error::DigestParseError;
pub use validator::Validator;
