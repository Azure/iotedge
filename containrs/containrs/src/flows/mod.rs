//! High-level flows for interacting with container registries.
//! These can (and should!) be used as jumping off points for lower-level
//! integrations.

pub(crate) mod download_image;

pub use download_image::{download_image, ImageDownload};
