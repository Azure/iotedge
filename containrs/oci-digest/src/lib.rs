//! Common digest package used across the OCI ecosystem, as described in the
//! oci-image spec.
//!
//! See https://github.com/opencontainers/image-spec/blob/master/descriptor.md#digests for details.
//!
//! Inspired by https://github.com/opencontainers/go-digest
//!
//! TODO: Add code to create digests (as opposed to only parsing and using them)

mod digest;
mod error;
mod validator;

#[cfg(feature = "serde")]
mod serde_impl;

pub use digest::Digest;
pub use error::DigestParseError;
pub use validator::{AlgorithmKind, Validator};
