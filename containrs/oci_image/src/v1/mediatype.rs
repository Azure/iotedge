/// specifies the media type for a content descriptor.
pub const DESCRIPTOR: &str = "application/vnd.oci.descriptor.v1+json";
/// specifies the media type for the oci-layout.
pub const LAYOUT_HEADER: &str = "application/vnd.oci.layout.header.v1+json";
/// specifies the media type for an image manifest.
pub const IMAGE_MANIFEST: &str = "application/vnd.oci.image.manifest.v1+json";
/// specifies the media type for an image index.
pub const IMAGE_INDEX: &str = "application/vnd.oci.image.index.v1+json";
/// used for layers referenced by the manifest.
pub const IMAGE_LAYER: &str = "application/vnd.oci.image.layer.v1.tar";
/// used for gzipped layers referenced by the manifest.
pub const IMAGE_LAYER_GZIP: &str = "application/vnd.oci.image.layer.v1.tar+gzip";
/// zstd compressed layers referenced by the manifest.
pub const IMAGE_LAYER_ZSTD: &str = "application/vnd.oci.image.layer.v1.tar+zstd";
/// layers referenced by the manifest but with distribution restrictions.
pub const IMAGE_LAYER_NON_DISTRIBUTABLE: &str =
    "application/vnd.oci.image.layer.nondistributable.v1.tar";
/// gzipped layers referenced by the manifest but with distribution
/// restrictions.
pub const IMAGE_LAYER_NON_DISTRIBUTABLE_GZIP: &str =
    "application/vnd.oci.image.layer.nondistributable.v1.tar+gzip";
/// zstd compressed layers referenced by the manifest but with distribution
/// restrictions.
pub const IMAGE_LAYER_NON_DISTRIBUTABLE_ZSTD: &str =
    "application/vnd.oci.image.layer.nondistributable.v1.tar+zstd";
/// specifies the media type for the image configuration.
pub const IMAGE_CONFIG: &str = "application/vnd.oci.image.config.v1+json";
