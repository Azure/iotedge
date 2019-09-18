use std::collections::HashMap;

use serde::{Deserialize, Serialize};

use super::Descriptor;
use super::{media_type, MediaType};

/// Index references manifests for various platforms.
/// This structure provides `application/vnd.oci.image.index.v1+json` mediatype
/// when marshalled to JSON.
#[derive(Debug, Serialize, Deserialize)]
struct Index {
    /// Manifests references platform specific manifests.
    #[serde(rename = "manifests")]
    pub manifests: Vec<Descriptor>,

    /// Annotations contains arbitrary metadata for the image index.
    #[serde(rename = "annotations", skip_serializing_if = "Option::is_none")]
    pub annotations: Option<HashMap<String, String>>,
}

impl MediaType for Index {
    const MEDIA_TYPE: &'static str = media_type::IMAGE_INDEX;
    const SIMILAR_MEDIA_TYPES: &'static [&'static str] =
        &["application/vnd.docker.distribution.manifest.list.v2+json"];
}
