use serde::{de, Deserialize, Deserializer, Serialize};

use super::{media_type, MediaType};
use super::{Annotations, Descriptor};

/// Manifest provides `application/vnd.oci.image.manifest.v1+json` mediatype
/// structure when marshalled to JSON.
#[derive(Debug, Serialize, Deserialize)]
pub struct Manifest {
    /// This REQUIRED property specifies the image manifest schema version. For
    /// this version of the specification, this MUST be 2 to ensure backward
    /// compatibility with older versions of Docker. The value of this field
    /// will not change. This field MAY be removed in a future version of the
    /// specification.
    #[serde(rename = "schemaVersion", deserialize_with = "validate_schema_is_2")]
    pub schema_version: i32,

    /// This property is reserved for use, to maintain compatibility. When used,
    /// this field contains the media type of this document, which differs from
    /// the descriptor use of mediaType.
    #[serde(rename = "mediaType", skip_serializing_if = "Option::is_none")]
    pub media_type: Option<String>,

    /// Config references a configuration object for a container, by digest.
    /// The referenced configuration object is a JSON blob that the runtime uses
    /// to set up the container.
    #[serde(rename = "config")]
    pub config: Descriptor,

    /// Layers is an indexed list of layers referenced by the manifest.
    #[serde(rename = "layers")]
    pub layers: Vec<Descriptor>,

    /// Annotations contains arbitrary metadata for the image manifest.
    #[serde(rename = "annotations", skip_serializing_if = "Option::is_none")]
    pub annotations: Option<Annotations>,
}

impl MediaType for Manifest {
    const MEDIA_TYPE: &'static str = media_type::IMAGE_MANIFEST;
    const SIMILAR_MEDIA_TYPES: &'static [&'static str] =
        &["application/vnd.docker.distribution.manifest.v2+json"];
}

fn validate_schema_is_2<'de, D>(des: D) -> Result<i32, D::Error>
where
    D: Deserializer<'de>,
{
    match i32::deserialize(des)? {
        2 => Ok(2),
        _ => Err(de::Error::custom(
            "v1::Manifest must have .schemaVersion = 2",
        )),
    }
}
