use serde::{Deserialize, Serialize};

use super::{media_type, MediaType};

/// The file name of oci image layout file
pub const FILE: &str = "oci-layout";
/// The version of ImageLayout
pub const VERSION: &str = "1.0.0";

/// OciLayout is the structure of the "oci-layout" file, found in the root
/// of an OCI Image-layout directory.
#[derive(Debug, Serialize, Deserialize)]
pub struct OciLayout {
    #[serde(rename = "imageLayoutVersion")]
    pub version: String,
}

impl MediaType for OciLayout {
    const MEDIA_TYPE: &'static str = media_type::LAYOUT_HEADER;
    const SIMILAR_MEDIA_TYPES: &'static [&'static str] = &[];
}
