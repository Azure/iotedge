//! Types and constants used by the [OCI Image Spec](https://github.com/opencontainers/image-spec)

pub mod v1;

/// Utility trait to associate structs with their media types (and similar media
/// types)
pub trait MediaType {
    /// OCI spec media type
    const MEDIA_TYPE: &'static str;
    /// Compatible media types (can be empty)
    const SIMILAR_MEDIA_TYPES: &'static [&'static str];
}
