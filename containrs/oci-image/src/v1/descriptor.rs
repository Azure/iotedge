use serde::{Deserialize, Serialize};

use oci_digest::Digest;

use super::{media_type, MediaType};
use super::{Annotations, Platform};

/// Descriptor describes the disposition of targeted content.
/// This structure provides `application/vnd.oci.descriptor.v1+json` mediatype
/// when marshalled to JSON.
#[derive(Debug, Serialize, Deserialize)]
pub struct Descriptor {
    /// MediaType is the media type of the object this schema refers to.
    ///
    /// Values MUST comply with RFC 6838, including the naming requirements in
    /// its section 4.2.
    // TODO?: validate raw Strings for media-type format compliance
    #[serde(rename = "mediaType")]
    pub media_type: String,

    /// Digest is the digest of the targeted content.
    #[serde(rename = "digest")]
    pub digest: Digest,

    /// Size specifies the size in bytes of the blob.
    ///
    /// This property exists so that a client will have an expected size for the
    /// content before processing. If the length of the retrieved content does
    /// not match the specified length, the content SHOULD NOT be trusted.
    #[serde(rename = "size")]
    pub size: i64,

    /// URLs specifies a list of URLs from which this object MAY be downloaded
    ///
    /// Each entry MUST conform to RFC 3986. Entries SHOULD use the http and
    /// https schemes, as defined in RFC 7230.
    #[serde(rename = "urls", skip_serializing_if = "Option::is_none")]
    pub urls: Option<Vec<String>>,

    /// Annotations contains arbitrary metadata relating to the targeted
    /// content.
    #[serde(rename = "annotations", skip_serializing_if = "Option::is_none")]
    pub annotations: Option<Annotations>,

    /// Platform describes the platform which the image in the manifest runs on.
    ///
    /// This should only be used when referring to a manifest.
    #[serde(rename = "platform", skip_serializing_if = "Option::is_none")]
    pub platform: Option<Platform>,

    /// This property is RESERVED for future versions of the specification.
    #[serde(
        rename = "data",
        skip_serializing,
        default,
        deserialize_with = "crate::reserved_field"
    )]
    pub data: Option<String>,
}

impl MediaType for Descriptor {
    const MEDIA_TYPE: &'static str = media_type::DESCRIPTOR;
    const SIMILAR_MEDIA_TYPES: &'static [&'static str] = &[];
}
