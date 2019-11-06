use serde::{Deserialize, Serialize};

use super::{media_type, Annotations, Descriptor, SchemaVersion2};
use crate::MediaType;

/// Index references manifests for various platforms.
/// This structure provides `application/vnd.oci.image.index.v1+json` mediatype
/// when marshalled to JSON.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Index {
    /// This REQUIRED property specifies the image manifest schema version. For
    /// this version of the specification, this MUST be 2 to ensure backward
    /// compatibility with older versions of Docker. The value of this field
    /// will not change. This field MAY be removed in a future version of the
    /// specification.
    #[serde(rename = "schemaVersion")]
    pub schema_version: SchemaVersion2,

    /// Manifests references platform specific manifests.
    #[serde(rename = "manifests")]
    pub manifests: Vec<Descriptor>,

    /// This property is reserved for use, to maintain compatibility.
    /// When used, this field contains the media type of this document.
    #[serde(rename = "mediaType", skip_serializing)]
    pub media_type: Option<String>,

    /// Annotations contains arbitrary metadata for the image index.
    #[serde(rename = "annotations", skip_serializing_if = "Option::is_none")]
    pub annotations: Option<Annotations>,
}

impl MediaType for Index {
    const MEDIA_TYPE: &'static str = media_type::IMAGE_INDEX;
    const SIMILAR_MEDIA_TYPES: &'static [&'static str] =
        &["application/vnd.docker.distribution.manifest.list.v2+json"];
}
