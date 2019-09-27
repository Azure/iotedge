use docker_reference::Reference;
use failure::{Context, Fail, ResultExt};
use hyper::client::connect::Connect;
use log::*;
use oci_image::v1 as ociv1;

use crate::Client;
use crate::{ErrorKind, Result};

#[derive(Debug, Fail)]
pub enum Error {
    #[fail(display = "Server returned malformed `manifest.json`")]
    MalformedManifestJson,
}

impl From<Error> for crate::Error {
    fn from(e: Error) -> Self {
        ErrorKind::DownloadImage(e).into()
    }
}

impl From<Context<Error>> for crate::Error {
    fn from(inner: Context<Error>) -> Self {
        inner.map(ErrorKind::DownloadImage).into()
    }
}

/// Collection of [`hyper::Body`]s corresponding to an image's manifest, config,
/// and layers.
pub struct ImageDownload {
    pub manifest_digest: String,
    pub manifest_json: hyper::Body,
    pub config_json: hyper::Body,
    pub layers: Vec<(hyper::Body, ociv1::Descriptor)>,
}

/// Given an `image` [`Reference`], returns a [ImageDownload] struct which
/// contains the image's manifest, config, and layers as streaming
/// [`hyper::Body`]s
pub async fn download_image<'a>(
    client: &mut Client<impl Connect + 'static>,
    image: &Reference,
) -> Result<ImageDownload> {
    // fetch manifest
    let (manifest_json, manifest_digest) = client.get_raw_manifest(image).await?;

    debug!("Fetched manifest.json");

    // validate manifest
    let manifest = serde_json::from_slice::<ociv1::Manifest>(&manifest_json)
        .context(Error::MalformedManifestJson)?;

    // fetch config
    let config_json = client
        .get_raw_blob(image.repo(), &manifest.config.digest)
        .await?;

    debug!("Kicked off config.json download");

    let mut layers = Vec::new();
    for layer in manifest.layers {
        let digest = layer.digest.clone();
        layers.push((
            client.get_raw_blob(image.repo(), &layer.digest).await?,
            layer,
        ));
        debug!("Kicked off layer {} download", digest);
    }

    Ok(ImageDownload {
        manifest_digest,
        manifest_json: manifest_json.into(),
        config_json,
        layers,
    })
}
