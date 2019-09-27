use std::collections::HashMap;
use std::fs::{self, File};
use std::io::Write;
use std::path::Path;

use failure::{Context, Fail, ResultExt};
use hyper::client::connect::Connect;
use lazy_static::lazy_static;

use docker_reference::Reference;
use oci_image::v1 as ociv1;

use crate::Client;
use crate::{Error as ContainrsError, ErrorKind, Result};

#[derive(Debug, Fail)]
pub enum Error {
    #[fail(display = "Specified output directory does not exist")]
    OutDirDoesNotExist,

    #[fail(display = "Could not create image directory")]
    CreateImageDir,

    #[fail(display = "Error while writing to `manifest.json`")]
    WriteManifestJson,

    #[fail(display = "Server returned malformed `manifest.json`")]
    MalformedManifestJson,

    #[fail(display = "Error while writing to `config.json`")]
    WriteConfigJson,

    #[fail(display = "Server returned malformed `config.json`")]
    MalformedConfigJson,
}

impl From<Error> for ContainrsError {
    fn from(e: Error) -> Self {
        ErrorKind::DownloadImage(e).into()
    }
}

impl From<Context<Error>> for ContainrsError {
    fn from(inner: Context<Error>) -> Self {
        inner.map(ErrorKind::DownloadImage).into()
    }
}

lazy_static! {
    static ref MEDIA_TYPE_TO_FILE_EXT: HashMap<&'static str, &'static str> = {
        use ociv1::media_type::*;
        let mut m: HashMap<&str, &str> = HashMap::new();
        m.insert(IMAGE_LAYER, "tar");
        m.insert(IMAGE_LAYER_GZIP, "tar.gz");
        m.insert(IMAGE_LAYER_GZIP_DOCKER, "tar.gz");
        m.insert(IMAGE_LAYER_ZSTD, "tar.zst");
        m.insert(IMAGE_LAYER_NON_DISTRIBUTABLE, "tar");
        m.insert(IMAGE_LAYER_NON_DISTRIBUTABLE_GZIP, "tar.gz");
        m.insert(IMAGE_LAYER_NON_DISTRIBUTABLE_ZSTD, "tar.zst");
        m
    };
}

/// Download an image (manifest, config, layers) into a folder.
/// Optionally validates all downloaded data (ensuring valid JSON, matching
/// digests, etc...)
pub async fn download_image(
    client: &mut Client<impl Connect + 'static>,
    image: &Reference,
    out_dir: &Path,
    validate: bool,
) -> Result<()> {
    if !out_dir.exists() {
        return Err(Error::OutDirDoesNotExist.into());
    }

    // fetch manifest
    let (raw_manifest, digest) = client.get_raw_manifest(image).await?;

    // create an output directory based on the manifest's digest
    let out_dir = out_dir.join(digest.replace(':', "-"));
    fs::create_dir(&out_dir).context(Error::CreateImageDir)?;

    // dump manifest.json to disk
    File::create(out_dir.join("manifest.json"))
        .and_then(|mut f| f.write(&raw_manifest))
        .context(Error::WriteManifestJson)?;

    // validate manifest
    let manifest = serde_json::from_slice::<ociv1::Manifest>(&raw_manifest)
        .context(Error::MalformedManifestJson)?;

    // fetch config
    let raw_config = client
        .get_raw_blob(image.repo(), &manifest.config.digest)
        .await?;

    File::create(out_dir.join("config.json"))
        .and_then(|mut f| f.write(&raw_config))
        .context(Error::WriteConfigJson)?;

    if validate {
        // validate config
        let _ = serde_json::from_slice::<ociv1::Image>(&raw_config)
            .context(Error::MalformedConfigJson)?;
    }

    // XXX: this "async" code is completely synchronous!
    // The client API needs to be reworked to split all the (&mut self) methods
    // into multiple smaller methods, where the long-running tasks (i.e: downloading
    // files) take the client as a (&self).
    // Or, at least that's the only solution I can think of.
    for layer in manifest.layers {
        let data = client.get_raw_blob(image.repo(), &layer.digest).await?;

        let filename = format!(
            "{}.{}",
            layer.digest.replace(':', "-"),
            MEDIA_TYPE_TO_FILE_EXT
                .get(layer.media_type.as_str())
                .unwrap_or(&"unknown")
        );

        File::create(out_dir.join(filename))
            .and_then(|mut f| f.write(&data))
            .context(Error::WriteConfigJson)?;
    }

    // TODO: validate downloaded images with their digests

    Ok(())
}
