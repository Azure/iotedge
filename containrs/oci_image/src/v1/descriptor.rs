use std::collections::HashMap;

use serde::{Deserialize, Serialize};

use super::{media_type, MediaType};

/// Descriptor describes the disposition of targeted content.
/// This structure provides `application/vnd.oci.descriptor.v1+json` mediatype
/// when marshalled to JSON.
#[derive(Debug, Serialize, Deserialize)]
pub struct Descriptor {
    /// MediaType is the media type of the object this schema refers to.
    #[serde(rename = "mediaType")]
    pub media_type: String,

    /// Digest is the digest of the targeted content.
    // TODO: replace digest string with an actual digest structure
    #[serde(rename = "digest")]
    pub digest: String,

    /// Size specifies the size in bytes of the blob.
    #[serde(rename = "size")]
    pub size: i64,

    /// URLs specifies a list of URLs from which this object MAY be downloaded
    #[serde(rename = "urls", skip_serializing_if = "Option::is_none")]
    pub urls: Option<Vec<String>>,

    /// Annotations contains arbitrary metadata relating to the targeted
    /// content.
    #[serde(rename = "annotations", skip_serializing_if = "Option::is_none")]
    pub annotations: Option<HashMap<String, String>>,

    /// Platform describes the platform which the image in the manifest runs on.
    ///
    /// This should only be used when referring to a manifest.
    #[serde(rename = "platform", skip_serializing_if = "Option::is_none")]
    pub platform: Option<Platform>,
}

impl MediaType for Descriptor {
    const MEDIA_TYPE: &'static str = media_type::DESCRIPTOR;
    const SIMILAR_MEDIA_TYPES: &'static [&'static str] = &[];
}

/// Platform describes the platform which the image in the manifest runs on.
#[derive(Debug, Serialize, Deserialize)]
pub struct Platform {
    /// Architecture field specifies the CPU architecture, for example
    /// `amd64` or `ppc64`.
    #[serde(rename = "architecture")]
    pub architecture: String,

    /// OS specifies the operating system, for example `linux` or `windows`.
    #[serde(rename = "os")]
    pub os: String,

    /// OSVersion is an optional field specifying the operating system
    /// version, for example on Windows `10.0.14393.1066`.
    #[serde(rename = "os.version", skip_serializing_if = "Option::is_none")]
    pub os_version: Option<String>,

    /// OSFeatures is an optional field specifying an array of strings,
    /// each listing a required OS feature (for example on Windows `win32k`).
    #[serde(rename = "os.features", skip_serializing_if = "Option::is_none")]
    pub os_features: Option<Vec<String>>,

    /// Variant is an optional field specifying a variant of the CPU, for
    /// example `v7` to specify ARMv7 when architecture is `arm`.
    #[serde(rename = "variant", skip_serializing_if = "Option::is_none")]
    pub variant: Option<String>,
}
