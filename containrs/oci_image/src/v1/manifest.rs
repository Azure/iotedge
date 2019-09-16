use std::collections::HashMap;

use serde::{Deserialize, Serialize};

use super::Descriptor;
use super::{media_type, MediaType};

/// Manifest provides `application/vnd.oci.image.manifest.v1+json` mediatype
/// structure when marshalled to JSON.
#[derive(Debug, Serialize, Deserialize)]
pub struct Manifest {
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
    pub annotations: Option<HashMap<String, String>>,
}

impl MediaType for Manifest {
    const MEDIA_TYPE: &'static str = media_type::IMAGE_MANIFEST;
    const SIMILAR_MEDIA_TYPES: &'static [&'static str] =
        &["application/vnd.docker.distribution.manifest.v2+json"];
}
