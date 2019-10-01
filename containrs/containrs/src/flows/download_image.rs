use failure::ResultExt;
use futures::future;

use log::*;

use docker_reference::Reference;
use oci_image::v1 as ociv1;

use crate::{Blob, Client};
use crate::{ErrorKind, Result};

/// Collection of [`Blob`]s corresponding to an image's manifest, config,
/// and layers.
pub struct ImageDownload {
    pub manifest_digest: String,
    pub manifest_json: Blob,
    pub config_json: Blob,
    pub layers: Vec<(Blob, ociv1::Descriptor)>,
}

/// Given an `image` [`Reference`], returns a [ImageDownload] struct which
/// contains the image's manifest, config, and layers as streaming
/// [`Blob`]s
pub async fn download_image(client: &Client, image: &Reference) -> Result<ImageDownload> {
    // fetch manifest
    let (manifest_json, manifest_digest) = client.get_raw_manifest(image).await?;
    let manifest_json = manifest_json.bytes().await?;

    debug!("Fetched manifest.json");

    // validate manifest
    let manifest = serde_json::from_slice::<ociv1::Manifest>(&manifest_json)
        .context("while parsing manifest.json")
        .context(ErrorKind::ApiMalformedJSON)?;

    let mut futures = Vec::new();

    futures.extend(
        manifest
            .layers
            .iter()
            .map(|layer| client.get_raw_blob(image.repo(), &layer.digest)),
    );
    futures.push(client.get_raw_blob(image.repo(), &manifest.config.digest));

    let mut layers = future::try_join_all(futures).await?;

    debug!("Fired off all download requests");

    let config_json = layers.pop().unwrap();

    Ok(ImageDownload {
        manifest_digest,
        manifest_json: Blob::new_immediate(manifest_json),
        config_json,
        layers: layers.into_iter().zip(manifest.layers).collect(),
    })
}
