//! High-level flows for interacting with container registries.
pub(crate) mod download_image;

pub use download_image::{download_image, ImageDownload};
