use std::collections::HashMap;

use futures::prelude::*;
use log::*;
use tokio::sync::oneshot;

use containrs::oci_image::v1::{Descriptor, Manifest};
use containrs::{Blob, Client as RegistryClient, Credentials, Digest, Reference};

use containerd_grpc::containerd::{
    services::{
        content::v1::{
            client::ContentClient, InfoRequest, StatusRequest, WriteAction, WriteContentRequest,
        },
        images::v1::{client::ImagesClient, CreateImageRequest, Image, UpdateImageRequest},
    },
    types::Descriptor as GrpcDescriptor,
};
use shellrt_api::v0::{request, response};

use crate::error::*;
use crate::util::*;

pub struct ImgPullHandler {
    grpc_uri: String,
}

impl ImgPullHandler {
    pub fn new(grpc_uri: String) -> ImgPullHandler {
        ImgPullHandler { grpc_uri }
    }

    pub async fn handle(
        self,
        req: request::ImgPull,
    ) -> Result<(response::ImgPull, Option<crate::ResponseThunk>)> {
        let request::ImgPull { image, credentials } = req;

        // parse image reference
        let image = image
            .parse::<Reference>()
            .context(ErrorKind::MalformedReference)?;

        // setup containrs client
        let credentials = match (credentials.get("username"), credentials.get("password")) {
            (Some(user), Some(pass)) => Credentials::UserPass(user.clone(), pass.clone()),
            (None, None) => Credentials::Anonymous,
            _ => return Err(ErrorKind::MalformedCredentials.into()),
        };
        // TODO?: specify registry client scheme (http vs. https)
        let registry_client = RegistryClient::new("https", image.registry(), credentials)
            .context(ErrorKind::MalformedReference)?;

        // setup grpc clients
        let (content_client, images_client) = (
            ContentClient::connect(self.grpc_uri.clone())
                .await
                .context(ErrorKind::GrpcConnect)?,
            ImagesClient::connect(self.grpc_uri.clone())
                .await
                .context(ErrorKind::GrpcConnect)?,
        );

        // fetch manifest
        let manifest_blob = registry_client
            .get_raw_manifest(&image)
            .await
            .context(ErrorKind::RegistryError)?;
        let manifest_descriptor = manifest_blob.descriptor().clone();
        let manifest_json = manifest_blob
            .bytes()
            .await
            .context(ErrorKind::RegistryError)?;
        let manifest = serde_json::from_slice::<Manifest>(&manifest_json)
            .context(ErrorKind::MalformedManifest)?;
        info!(
            "downloaded manifest.json ({:?})",
            manifest_descriptor.digest
        );

        let required_descriptors = manifest
            .layers
            .iter()
            .chain(std::iter::once(&manifest.config));

        // download any blobs (or blob parts) that are missing in the containerd
        // content store
        let containerd_download_futs = required_descriptors
            .map(|descriptor| {
                let registry_client = &registry_client;
                let content_client = &content_client;
                let image = &image;
                async move {
                    // If it's already in containerd, no need to download it!
                    if already_in_containerd(content_client.clone(), &descriptor.digest).await? {
                        return Ok(());
                    }

                    // If not, check if the blob happens to be partially downloaded.
                    let partial_offset =
                        partially_in_containerd(content_client.clone(), &descriptor.digest).await?;
                    // if not, download the entire file (i.e: offset = 0)
                    let offset = partial_offset.unwrap_or(0);

                    // kick off blob download
                    let blob = registry_client
                        .get_blob_part(image.repo(), &descriptor, offset..)
                        .await
                        .context(ErrorKind::RegistryError)?;

                    // stream the blob into containerd
                    stream_to_containerd(content_client.clone(), blob, offset, HashMap::new()).await
                }
            })
            .collect::<Vec<_>>();

        // Don't forget about the manifest!
        let containerd_manifest_download_fut = async {
            if already_in_containerd(content_client.clone(), &manifest_descriptor.digest).await? {
                return Ok(());
            }

            let blob = Blob::new_immediate(manifest_json, manifest_descriptor.clone());
            // must specify dependencies via tags, or else containerd will helpfully delete
            // image artifacts at certain intervals
            let labels = manifest
                .layers
                .iter()
                .chain(std::iter::once(&manifest.config))
                .enumerate()
                .map(|(i, descriptor)| {
                    (
                        format!("containerd.io/gc.ref.content.{}", i),
                        descriptor.digest.to_string(),
                    )
                })
                .collect();

            stream_to_containerd(content_client.clone(), blob, 0, labels).await
        };

        info!("downloading image contents...");
        future::try_join(
            containerd_manifest_download_fut,
            future::try_join_all(containerd_download_futs),
        )
        .await?;
        info!("image downloaded successfully!");

        register_image_with_containerd(images_client, &image, &manifest_descriptor).await?;

        Ok((response::ImgPull {}, None))
    }
}

/// Check if a blob already exists in containerd's content store
pub(crate) async fn already_in_containerd(
    mut content_client: ContentClient<tonic::transport::Channel>,
    digest: &Digest,
) -> Result<bool> {
    let req = tonic::Request::new_namespaced(InfoRequest {
        digest: digest.to_string(),
    });

    let res = content_client.info(req).await;

    match res {
        Ok(_) => {
            // we don't actually care about the info, just if it exists or not
            Ok(true)
        }
        Err(e) => match e.code() {
            tonic::Code::NotFound => Ok(false),
            _ => Err(e.context(ErrorKind::GrpcUnexpectedErr).into()),
        },
    }
}

/// Check if a blob has been partially downloaded to containerd, returning the
/// downloaded offset if applicable.
async fn partially_in_containerd(
    mut content_client: ContentClient<tonic::transport::Channel>,
    digest: &Digest,
) -> Result<Option<u64>> {
    let req = tonic::Request::new_namespaced(StatusRequest {
        r#ref: digest.to_string(),
    });

    let res = content_client.status(req).await;

    let offset = match res {
        Ok(res) => {
            // `status` is only an `Option` type because of funky protobuf codegen
            let status = res
                .into_inner()
                .status
                .expect("containerd grpc api returned null status");
            Some(status.offset as u64)
        }
        Err(e) => match e.code() {
            tonic::Code::NotFound => None,
            _ => return Err(e.context(ErrorKind::GrpcUnexpectedErr).into()),
        },
    };

    Ok(offset)
}

async fn stream_to_containerd(
    mut content_client: ContentClient<tonic::transport::Channel>,
    blob: Blob,
    offset: u64,
    labels: HashMap<String, String>,
) -> Result<()> {
    // why two digests? combinators.
    let digest = blob.descriptor().digest.clone();
    let digest_2 = digest.clone();
    let total_len = blob.descriptor().size;

    // If the download fails mid-way through the transfer, the error will be sent
    // along this oneshot channel.
    let (bail_tx, bail_rx) = oneshot::channel();
    let mut bail_tx = Some(bail_tx);
    let mut bail_rx = bail_rx.fuse();

    // offset will get updated on each incoming request
    let mut offset = offset as usize;

    let write_request_stream = blob
        .into_stream()
        // repackage incoming data into a WriteContentRequest
        .map_ok(move |data| {
            let req = WriteContentRequest {
                action: WriteAction::Write as i32,
                offset: offset as i64,
                data: data.to_vec(),
                r#ref: digest_2.to_string(),
                // These aren't actually used, but the protobuf codegen didn't mark them as optional
                total: 0,
                expected: "".to_string(),
                labels: HashMap::new(),
            };
            offset += data.len();
            req
        })
        .or_else(move |err| {
            if let Some(tx) = bail_tx.take() {
                tx.send(err).expect("channel was dropped")
            }
            future::pending::<std::result::Result<_, ()>>()
        })
        // will never panic, since the or_else converts all errors into future::pending
        .map(|res| res.unwrap())
        // cap off the stream with one final "commit" message
        .chain(stream::once(async move {
            WriteContentRequest {
                action: WriteAction::Commit as i32,
                total: total_len,
                offset: total_len,
                // TODO?: add toggle to disable verification
                expected: digest.to_string(),
                // These aren't actually used, but the protobuf codegen didn't mark them as optional
                r#ref: digest.to_string(),
                data: Vec::new(),
                labels,
            }
        }))
        .fuse();

    // kick off the streaming gRPC call
    let request = tonic::Request::new_namespaced(write_request_stream);
    let mut response_stream = content_client
        .write(request)
        .await
        .context(ErrorKind::GrpcUnexpectedErr)?
        .into_inner();

    loop {
        match future::select(&mut bail_rx, response_stream.next()).await {
            future::Either::Left((rx_err, _)) => {
                match rx_err {
                    Err(_) => {
                        // The bail channel getting dropped is totally fine. It
                        // happens once the blob stream is exhausted
                    }
                    Ok(containrs_err) => {
                        return Err(containrs_err.context(ErrorKind::RegistryError).into())
                    }
                }
            }
            future::Either::Right((item, _)) => {
                match item {
                    Some(grpc_res) => match grpc_res {
                        Ok(write_res) => {
                            if write_res.action == WriteAction::Commit as i32 {
                                // TODO?: do something interesting with the last (commit) response
                                debug!("{:#?}", write_res);
                                return Ok(());
                            }
                        }
                        Err(e) => match e.code() {
                            _ => return Err(e.context(ErrorKind::GrpcUnexpectedErr).into()),
                        },
                    },
                    None => return Ok(()),
                };
            }
        }
    }
}

async fn register_image_with_containerd(
    mut images_client: ImagesClient<tonic::transport::Channel>,
    image: &Reference,
    manifest_descriptor: &Descriptor,
) -> Result<()> {
    let now = std::time::SystemTime::now();
    let image = || {
        let manifest_descriptor = manifest_descriptor.clone();
        Image {
            name: if image.registry() == "registry-1.docker.io" {
                // for some reason, containerd-cri tags images downloaded from
                // "registry-1.docker.io" as being pulled from "docker.io".
                let mut raw_image = image.clone().into_raw_reference();
                // cannot panic, since docker.io is a valid domain string
                raw_image.set_domain(Some("docker.io")).unwrap();
                raw_image.canonicalize().to_string()
            } else {
                image.to_string()
            },
            labels: HashMap::new(),
            target: Some(GrpcDescriptor {
                media_type: manifest_descriptor.media_type,
                digest: manifest_descriptor.digest.to_string(),
                size: manifest_descriptor.size,
                annotations: manifest_descriptor.annotations.unwrap_or_default(),
            }),
            created_at: Some(now.into()),
            updated_at: Some(now.into()),
        }
    };

    // first, attempt to update an existing image
    let update_image_req = UpdateImageRequest {
        image: Some(image()),
        update_mask: None,
    };
    let req = tonic::Request::new_namespaced(update_image_req);
    let res = images_client.update(req).await;
    match res {
        Ok(res) => {
            debug!("{:#?}", res.into_inner());
            return Ok(());
        }
        Err(e) => match e.code() {
            tonic::Code::NotFound => {
                // that's fine.
                // if the image doesn't exists, we'll create a new image
            }
            _ => return Err(e.context(ErrorKind::GrpcUnexpectedErr).into()),
        },
    }

    // if none exists, create a new image
    let create_image_req = CreateImageRequest {
        image: Some(image()),
    };
    let req = tonic::Request::new_namespaced(create_image_req);
    let res = images_client.create(req).await;
    match res {
        Ok(res) => {
            debug!("{:#?}", res.into_inner());
            Ok(())
        }
        Err(ref e) => match e.code() {
            _ => {
                res.context(ErrorKind::GrpcUnexpectedErr)?;
                unreachable!()
            }
        },
    }
}
