use std::collections::HashMap;
use std::path::{Path, PathBuf};

use failure::{Context, Fail, ResultExt};
use futures::future::{self, TryFutureExt};
use hyper::client::connect::Connect;
use lazy_static::lazy_static;
use log::*;
use tokio::fs::{self, File};
use tokio::prelude::*;

use docker_reference::Reference;
use oci_image::v1 as ociv1;

use crate::Client;
use crate::{Error as ContainrsError, ErrorKind as ContainrsErrorKind, Result};

#[derive(Debug, Fail)]
pub enum Error {
    #[fail(display = "Specified output directory does not exist")]
    OutDirDoesNotExist,

    #[fail(display = "Error while creating a directory")]
    CreateDir,

    #[fail(display = "Server returned malformed `manifest.json`")]
    MalformedManifestJson,

    #[fail(display = "Error while downloading blob")]
    DownloadError,

    #[fail(display = "Error while writing to file")]
    WriteError,
}

impl From<Error> for ContainrsError {
    fn from(e: Error) -> Self {
        ContainrsErrorKind::DownloadImage(e).into()
    }
}

impl From<Context<Error>> for ContainrsError {
    fn from(inner: Context<Error>) -> Self {
        inner.map(ContainrsErrorKind::DownloadImage).into()
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

    debug!("Fetched manifest.json");

    // validate manifest
    let manifest = serde_json::from_slice::<ociv1::Manifest>(&raw_manifest)
        .context(Error::MalformedManifestJson)?;

    // create an output directory based on the manifest's digest
    let out_dir = out_dir.join(digest.replace(':', "-"));
    fs::create_dir(&out_dir)
        .await
        .context(format!("{:?}", out_dir))
        .context(Error::CreateDir)?;

    // dump manifest.json to disk
    let manifest_path = out_dir.join("manifest.json");
    File::create(&manifest_path)
        .and_then(|mut f| async move { f.write(&raw_manifest).await })
        .await
        .context(format!("{:?}", manifest_path))
        .context(Error::WriteError)?;

    debug!("Dumped manifest.json to disk");

    // the rest of the resources can be downloaded in parallel

    let mut downloads = Vec::new();

    // fetch config
    downloads.push(write_chunk_stream_to_file(
        out_dir.join("config.json"),
        client
            .get_raw_blob(image.repo(), &manifest.config.digest)
            .await?,
    ));

    debug!("Kicked off config.json download");

    for layer in manifest.layers {
        let body = client.get_raw_blob(image.repo(), &layer.digest).await?;

        let filename = format!(
            "{}.{}",
            layer.digest.replace(':', "-"),
            MEDIA_TYPE_TO_FILE_EXT
                .get(layer.media_type.as_str())
                .unwrap_or(&"unknown")
        );

        debug!("Kicked off {} download", filename);

        downloads.push(write_chunk_stream_to_file(out_dir.join(filename), body))
    }

    future::try_join_all(downloads).await?;

    if validate {
        // TODO: validate downloaded files with their digests
        // (and JSON structure, if appropriate)
    }

    Ok(())
}

async fn write_chunk_stream_to_file(file_path: PathBuf, mut body: hyper::Body) -> Result<()> {
    let mut file = File::create(&file_path).await.context(Error::WriteError)?;

    while let Some(next) = body.next().await {
        let data = next.context(Error::DownloadError)?;
        file.write(data.as_ref()).await.context(Error::WriteError)?;
    }
    Ok(())
}
