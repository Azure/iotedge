use serde::{Deserialize, Serialize};

use super::{media_type, MediaType};
use super::{Annotations, Descriptor};

/// Index references manifests for various platforms.
/// This structure provides `application/vnd.oci.image.index.v1+json` mediatype
/// when marshalled to JSON.
#[derive(Debug, Serialize, Deserialize)]
struct Index {
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

/// Platform describes the platform which the image in the manifest runs on.
#[derive(Debug, Serialize, Deserialize)]
pub struct Platform {
    /// Architecture field specifies the CPU architecture, for example
    /// `amd64` or `ppc64`.
    ///
    /// Image indexes SHOULD use, and implementations SHOULD understand, values
    /// listed in the Go Language document for GOARCH.
    #[serde(rename = "architecture")]
    pub architecture: String,

    /// OS specifies the operating system, for example `linux` or `windows`.
    ///
    /// Image indexes SHOULD use, and implementations SHOULD understand, values
    /// listed in the Go Language document for GOOS.
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
        deserialize_with = "crate::reserved_field"
    )]
    pub features: Option<Vec<String>>,
}
