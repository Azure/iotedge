use serde::{Deserialize, Serialize};

/// The file name of oci image layout file
pub const FILE: &str = "oci-layout";
/// The version of ImageLayout
pub const VERSION: &str = "1.0.0";

/// ImageLayout is the structure in the "oci-layout" file, found in the root
/// of an OCI Image-layout directory.
#[derive(Debug, Serialize, Deserialize)]
pub struct ImageLayout {
    #[serde(rename = "imageLayoutVersion")]
    pub version: String,
}
