use serde::{Deserialize, Serialize};

use oci_digest::Digest;

use super::Annotations;
use super::{media_type, MediaType};

/// Descriptor describes the disposition of targeted content.
/// This structure provides `application/vnd.oci.descriptor.v1+json` mediatype
/// when marshalled to JSON.
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
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
        deserialize_with = "crate::de_reserved_field"
    )]
    pub _data: Option<String>,
}

impl Descriptor {
    /// Create a new Descriptor by specifying only the strictly requires fields.
    pub fn new_base(media_type: String, digest: Digest, size: i64) -> Descriptor {
        Descriptor {
            media_type,
            digest,
            size,
            urls: None,
            annotations: None,
            platform: None,
            _data: None,
        }
    }

    /// Add an annotation to the Descriptor
    pub fn add_annotation(&mut self, key: &str, val: &str) {
        if self.annotations.is_none() {
            self.annotations = Some(Annotations::new());
        }

        self.annotations
            .as_mut()
            .unwrap()
            .insert(key.to_string(), val.to_string());
    }
}

impl MediaType for Descriptor {
    const MEDIA_TYPE: &'static str = media_type::DESCRIPTOR;
    const SIMILAR_MEDIA_TYPES: &'static [&'static str] = &[];
}

#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Platform {
    /// Architecture field specifies the CPU architecture, for example
    /// `amd64` or `ppc64`.
    ///
    /// Image indexes SHOULD use, and implementations SHOULD understand, values
    /// listed in the Go Language document for GOARCH.
    // TODO: this could be validated while deserializing
    #[serde(rename = "architecture")]
    pub architecture: String,

    /// OS specifies the operating system, for example `linux` or `windows`.
    ///
    /// Image indexes SHOULD use, and implementations SHOULD understand, values
    /// listed in the Go Language document for GOOS.
    // TODO: this could be validated while deserializing
    #[serde(rename = "os")]
    pub os: String,

    /// OSVersion is an optional field specifying the operating system
    /// version. Valid values are implementation-defined. e.g `10.0.14393.1066`
    /// on `windows`.
    #[serde(rename = "os.version", skip_serializing_if = "Option::is_none")]
    pub os_version: Option<String>,

    /// OSFeatures is an optional field specifying an array of strings,
    /// each listing a required OS feature.
    ///
    /// When os is windows, image indexes SHOULD use, and implementations
    /// SHOULD understand the following values:
    /// - win32k: image requires win32k.sys on the host (Note: win32k.sys is
    ///   missing on Nano Server)
    ///
    /// When os is not windows, values are implementation-defined and SHOULD be
    /// submitted to this specification for standardization.
    #[serde(rename = "os.features", skip_serializing_if = "Option::is_none")]
    pub os_features: Option<Vec<String>>,

    /// Variant is an optional field specifying a variant of the CPU, for
    /// example `v7` to specify ARMv7 when architecture is `arm`.
    ///
    /// When the variant of the CPU is not listed in the table, values are
    /// implementation-defined and SHOULD be submitted to this specification for
    /// standardization.
    ///
    /// ISA/ABI     | architecture | variant
    /// ------------|--------------|--------
    /// ARM 32-bit, | arm v6       | v6
    /// ARM 32-bit, | arm v7       | v7
    /// ARM 32-bit, | arm v8       | v8
    /// ARM 64-bit, | arm64 v8     | v8
    // TODO?: variant could be converted into a enum (with a catch-all variant)
    #[serde(rename = "variant", skip_serializing_if = "Option::is_none")]
    pub variant: Option<String>,

    /// This property is RESERVED for future versions of the specification.
    #[serde(
        rename = "features",
        skip_serializing,
        default,
        deserialize_with = "crate::de_reserved_field"
    )]
    pub _features: Option<Vec<String>>,
}
