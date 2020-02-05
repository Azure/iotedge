use serde::{Deserialize, Serialize};

use crate::v1::{media_type, Annotations, Descriptor, Index, Manifest};
use crate::MediaType;

use std::path::{Path, PathBuf};

/// The version of ImageLayout
pub const OCI_LAYOUT_VERSION: &str = "1.0.0";

/// Encodes the expected filesystem layout and metadata contents for OCI image
/// layouts. Does not perform any filesystem or network calls itself.
#[derive(Debug)]
pub struct ImageLayout {
    oci_layout: (PathBuf, OciLayout),
    index_json: (PathBuf, Index),
    blobs: Vec<(PathBuf, Descriptor)>,
}

/// A builder to iteratively construct [`ImageLayout`]s.
pub struct ImageLayoutBuilder {
    oci_layout: OciLayout,
    index_json: Index,
    blobs: Vec<(PathBuf, Descriptor)>,
}

impl Default for ImageLayoutBuilder {
    fn default() -> Self {
        Self::new()
    }
}

impl ImageLayoutBuilder {
    /// Create a new ImageLayoutBuilder
    pub fn new() -> ImageLayoutBuilder {
        ImageLayoutBuilder {
            oci_layout: OciLayout {
                version: OCI_LAYOUT_VERSION.into(),
            },
            index_json: Index::default(),
            blobs: Vec::new(),
        }
    }

    /// Add an annotation to index.json
    pub fn annotation(mut self, key: &str, val: &str) -> Self {
        if self.index_json.annotations.is_none() {
            self.index_json.annotations = Some(Annotations::new());
        }

        self.index_json
            .annotations
            .as_mut()
            .unwrap()
            .insert(key.to_string(), val.to_string());

        self
    }

    /// Add a manifest to the image layout.
    pub fn manifest(mut self, manifest: Manifest, manifest_descriptor: Descriptor) -> Self {
        // add the manifest descriptor to the index_json
        self.index_json.manifests.push(manifest_descriptor.clone());

        let mut blob_descriptors = Vec::new();

        // add the various descriptors referenced in the manifest
        // (and the manifest itself) as required blobs
        blob_descriptors.push(manifest_descriptor);
        blob_descriptors.push(manifest.config.clone());
        for descriptor in &manifest.layers {
            blob_descriptors.push(descriptor.clone());
        }

        self.blobs
            .extend(blob_descriptors.into_iter().map(|descriptor| {
                let mut path = PathBuf::new();
                path.push("blobs");
                path.push(descriptor.digest.algorithm());
                path.push(descriptor.digest.encoded());
                (path, descriptor)
            }));

        self
    }

    /// Add multiple manifests to the image layout.
    pub fn manifests<M>(mut self, manifests: M) -> Self
    where
        M: IntoIterator<Item = (Manifest, Descriptor)>,
    {
        for (manifest, descriptor) in manifests {
            self = self.manifest(manifest, descriptor);
        }

        self
    }

    /// Consumes the builder, and returns a new ImageLayout
    pub fn build(self) -> ImageLayout {
        ImageLayout {
            oci_layout: ("oci-layout".into(), self.oci_layout),
            index_json: ("index.json".into(), self.index_json),
            blobs: self.blobs,
        }
    }
}

impl ImageLayout {
    /// Return a new ImageLayoutBuilder
    pub fn builder() -> ImageLayoutBuilder {
        ImageLayoutBuilder::new()
    }

    /// Return the oci image layout standard path for `oci-layout`, alongside a
    /// reference to a [OciLayout] (which can be serialized to JSON)
    pub fn oci_layout(&self) -> (&Path, &OciLayout) {
        let (path, data) = &self.oci_layout;
        (path, data)
    }

    /// Return the oci image layout standard path for `index.json`, alongside a
    /// reference to a [Index] (which can be serialized to JSON).
    pub fn index_json(&self) -> (&Path, &Index) {
        let (path, data) = &self.index_json;
        (path, data)
    }

    /// Return an iterator over the blobs associated with the image and oci
    /// image layout standard path associated with it.
    pub fn blobs(&self) -> impl Iterator<Item = (&Path, &Descriptor)> {
        self.blobs.iter().map(|(p, d)| (p.as_path(), d))
    }
}

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
