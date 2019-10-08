//! Types and constants used by the [OCI Image Spec](https://github.com/opencontainers/image-spec)

pub mod v1;

/// Utility trait for associating Structs with media types
pub trait MediaType {
    /// OCI spec media type
    const MEDIA_TYPE: &'static str;
    /// Compatible media types (can be empty)
    const SIMILAR_MEDIA_TYPES: &'static [&'static str];
}

use serde::{de, Deserializer};

/// Serde `deserialize_with` helper for reserved fields
pub(crate) fn reserved_field<'de, D, T>(_des: D) -> Result<Option<T>, D::Error>
where
    D: Deserializer<'de>,
{
    Err(de::Error::custom(
        "This property is RESERVED for future versions of the specification",
    ))
}
