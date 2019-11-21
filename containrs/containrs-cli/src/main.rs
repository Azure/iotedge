#![allow(clippy::cognitive_complexity)]

use std::collections::HashMap;
use std::path::Path;
use std::time::Instant;

use clap::{App, AppSettings, Arg, SubCommand};
use failure::ResultExt;
use futures::future;
use indicatif::{MultiProgress, ProgressBar, ProgressStyle};
use lazy_static::lazy_static;
use tokio::fs::{self, File};
use tokio::prelude::*;

use containrs::oci_image::v1 as ociv1;
use containrs::{Blob, Client, Credentials, Paginate};
use containrs::{Reference, ReferenceKind};

mod parse_range;
use crate::parse_range::ParsableRange;

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

#[tokio::main]
async fn main() {
    if let Err(fail) = true_main().await {
        println!("{}", fail);
        for cause in fail.iter_causes() {
            println!("\tcaused by: {}", cause);
        }
    }
}

lazy_static! {
    static ref PB_STYLE: ProgressStyle = ProgressStyle::default_bar()
        .template("[{elapsed_precise}] {msg:16} - {total_bytes:8} {wide_bar} [{percent:3}%]",)
        .progress_chars("=>-");
}

async fn true_main() -> Result<(), failure::Error> {
    pretty_env_logger::init();

    let app_m = App::new("containrs-cli")
        .setting(AppSettings::SubcommandRequiredElseHelp)
        .version("0.1.0")
        .author("Azure IoT Edge Devs")
        .about("CLI for interacting with the containrs library.")
        .arg(
            Arg::with_name("transport-scheme")
                .help("Transport scheme (defaults to \"https\")")
                .long("transport-scheme")
                .takes_value(true),
        )
        .arg(
            Arg::with_name("username")
                .help("Username (for use with UserPass Credentials)")
                .short("u")
                .long("username")
                .takes_value(true),
        )
        .arg(
            Arg::with_name("password")
                .help("Password (for use with UserPass Credentials)")
                .short("p")
                .long("password")
                .takes_value(true),
        )
        .subcommand(
            SubCommand::with_name("raw")
                .setting(AppSettings::SubcommandRequiredElseHelp)
                .about("Retrieve raw responses from various endpoints")
                .subcommand(
                    SubCommand::with_name("catalog")
                        .about("Retrieve a sorted list of repositories available in the registry")
                        .arg(
                            Arg::with_name("registry")
                                .help("Registry to read catalog from")
                                .required(true)
                                .index(1),
                        )
                        .arg(
                            Arg::with_name("n")
                                .help("Paginate results into n-sized chunks")
                                .index(2),
                        ),
                )
                .subcommand(
                    SubCommand::with_name("tags")
                        .about("Retrieve a sorted list of tags under a given repository")
                        .arg(
                            Arg::with_name("image")
                                .help("An image reference (tag/digest is ignored)")
                                .required(true)
                                .index(1),
                        )
                        .arg(
                            Arg::with_name("n")
                                .help("Paginate results into n-sized chunks")
                                .index(2),
                        ),
                )
                .subcommand(
                    SubCommand::with_name("manifest")
                        .about("Retrieve an image's manifest")
                        .arg(
                            Arg::with_name("image")
                                .help("Image reference")
                                .required(true)
                                .index(1),
                        ),
                )
                .subcommand(
                    SubCommand::with_name("multimanifest")
                        .about("Retrieve one or more image manifests at once.")
                        .arg(
                            Arg::with_name("image")
                                .help("Image reference")
                                .required(true)
                                .index(1)
                                .multiple(true),
                        ),
                )
                .subcommand(
                    SubCommand::with_name("blob")
                        .about("Retrieve a blob from a given repository")
                        .arg(
                            Arg::with_name("image")
                                .help("Image reference (digest must be set)")
                                .required(true)
                                .index(1),
                        )
                        .arg(
                            Arg::with_name("range")
                                .help("A range of bytes to retrieve (e.g: \"10..\" will return everything except the first 9 bytes)")
                                .index(2),
                        ),
                ),
        )
        // TODO?: hide the test section in the CLI behind a feature flag
        .subcommand(
            SubCommand::with_name("test")
                .setting(AppSettings::SubcommandRequiredElseHelp)
                .about("Misc commands for testing / benchmarking containrs internals.")
                .subcommand(
                    SubCommand::with_name("multimanifest")
                        .about("Test scope-based caching performance by retrieving multiple image manifests in parallel.")
                        .arg(
                            Arg::with_name("image")
                                .help("Image reference")
                                .required(true)
                                .index(1)
                                .multiple(true),
                        ),
                )
        )
        .subcommand(
            SubCommand::with_name("download")
                .about("Downloads an image, and lays it out according to the OCI Image Layout standard")
                .arg(
                    Arg::with_name("image")
                        .help("Image reference")
                        .required(true)
                        .index(1),
                )
                .arg(
                    Arg::with_name("outdir")
                        .help("Output directory")
                        .required(true)
                        .index(2),
                )
                .arg(
                    Arg::with_name("skip-validate")
                        .help("Skip validating downloaded image digests")
                        .long("skip-validate")
                )
        )
        .get_matches();

    // TODO: throw these options into a Struct
    // TODO: support loading configuration from file

    let transport_scheme = app_m.value_of("transport-scheme").unwrap_or("https");

    let username = app_m.value_of("username");
    let password = app_m.value_of("password");

    let credentials = match (username, password) {
        (Some(user), Some(pass)) => Credentials::UserPass(user.to_string(), pass.to_string()),
        _ => Credentials::Anonymous,
    };

    match app_m.subcommand() {
        ("raw", Some(app_m)) => {
            match app_m.subcommand() {
                ("catalog", Some(sub_m)) => {
                    let registry = sub_m
                        .value_of("registry")
                        .expect("registry should be a required argument");

                    let init_paginate = match sub_m.value_of("n") {
                        Some(n) => Some(Paginate::new(n.parse()?, "".to_string())),
                        None => None,
                    };

                    let client = Client::new(transport_scheme, registry, credentials)?;

                    let mut paginate = init_paginate;
                    loop {
                        let (catalog, next_paginate) =
                            match client.get_raw_catalog(paginate).await? {
                                Some((catalog, next_paginate)) => (catalog, next_paginate),
                                None => {
                                    eprintln!("Registry doesn't support the _catalog endpoint");
                                    break;
                                }
                            };

                        tokio::io::stdout().write_all(&catalog).await?;

                        if next_paginate.is_none() {
                            break;
                        }

                        // quick and dirty "wait for enter" paging
                        use std::io::Read;
                        let _ = std::io::stdin().bytes().next();

                        paginate = next_paginate;
                    }
                }
                ("tags", Some(sub_m)) => {
                    let image = sub_m
                        .value_of("image")
                        .expect("image should be a required argument");

                    let image = image.parse::<Reference>()?;
                    eprintln!("canonical: {:#?}", image);

                    let init_paginate = match sub_m.value_of("n") {
                        Some(n) => Some(Paginate::new(n.parse()?, "".to_string())),
                        None => None,
                    };

                    let client = Client::new(transport_scheme, image.registry(), credentials)?;

                    let mut paginate = init_paginate;
                    loop {
                        let (tags, next_paginate) =
                            client.get_raw_tags(image.repo(), paginate).await?;

                        tokio::io::stdout().write_all(&tags).await?;

                        if next_paginate.is_none() {
                            break;
                        }

                        // quick and dirty "wait for enter" paging
                        use std::io::Read;
                        let _ = std::io::stdin().bytes().next();

                        paginate = next_paginate;
                    }
                }
                ("manifest", Some(sub_m)) => {
                    let image = sub_m
                        .value_of("image")
                        .expect("image should be a required argument");

                    let image = image.parse::<Reference>()?;
                    eprintln!("canonical: {:#?}", image);

                    let client = Client::new(transport_scheme, image.registry(), credentials)?;

                    let progress = ProgressBar::new(0);
                    progress.set_style(PB_STYLE.clone());
                    progress.set_message("manifest.json");

                    let mut manifest_blob = client.get_raw_manifest(&image).await?;
                    progress.println(format!(
                        "Server reported digest: {}",
                        manifest_blob.descriptor().digest
                    ));

                    progress.set_length(manifest_blob.len().unwrap_or(0));

                    // dump the blob to stdout, chunk by chunk
                    let mut validator = manifest_blob
                        .descriptor()
                        .digest
                        .validator()
                        .ok_or_else(|| failure::err_msg("unsupported digest algorithm"))?;
                    let mut stdout = tokio::io::stdout();
                    while let Some(data) = manifest_blob.chunk().await? {
                        validator.input(&data);
                        let bytes_written = stdout.write(data.as_ref()).await?;
                        progress.inc(bytes_written as u64);
                    }
                    progress.finish();

                    eprintln!(
                        "Calculated digest {} the expected digest",
                        if validator.validate() {
                            "matches"
                        } else {
                            "**does not** match"
                        }
                    );
                }
                ("blob", Some(sub_m)) => {
                    let image = sub_m
                        .value_of("image")
                        .expect("image should be a required argument");

                    let image = image.parse::<Reference>()?;
                    eprintln!("canonical: {:#?}", image);

                    let digest = match image.kind() {
                        ReferenceKind::Digest(digest) => digest,
                        _ => return Err(failure::err_msg("must specify digest")),
                    };

                    let client = Client::new(transport_scheme, image.registry(), credentials)?;

                    let progress = ProgressBar::new(0);
                    progress.set_style(PB_STYLE.clone());
                    progress.set_message(&digest.as_str().split(':').nth(1).unwrap()[..16]);

                    let mut blob = match sub_m.value_of("range") {
                        Some(s) => {
                            let range: ParsableRange<u64> = s.parse()?;
                            client
                                .get_blob_part_from_registry(image.repo(), digest, range)
                                .await?
                        }
                        None => client.get_blob_from_registry(image.repo(), digest).await?,
                    };

                    progress.set_length(blob.len().unwrap_or(0));

                    // dump the blob to stdout, chunk by chunk, validating it along the way
                    let mut validator = digest
                        .validator()
                        .ok_or_else(|| failure::err_msg("unsupported digest algorithm"))?;
                    let mut stdout = tokio::io::stdout();
                    while let Some(data) = blob.chunk().await? {
                        validator.input(&data);
                        let bytes_written = stdout.write(data.as_ref()).await?;
                        progress.inc(bytes_written as u64);
                    }
                    progress.finish();

                    eprintln!(
                        "Calculated digest {} the expected digest",
                        if validator.validate() {
                            "matches"
                        } else {
                            "**does not** match"
                        }
                    );
                }
                _ => unreachable!(),
            }
        }
        ("test", Some(app_m)) => match app_m.subcommand() {
            ("multimanifest", Some(sub_m)) => {
                let images = sub_m
                    .values_of("image")
                    .expect("image should be a required argument")
                    .map(|s| s.parse::<Reference>())
                    .collect::<Result<Vec<_>, _>>()?;

                eprintln!("canonical: {:#?}", images);

                let client = Client::new(
                    transport_scheme,
                    images.first().unwrap().registry(),
                    credentials,
                )?;

                let download_timer = Instant::now();

                let futures = images
                    .iter()
                    .map(|img| client.get_raw_manifest(img))
                    .collect::<Vec<_>>();
                let _ = future::try_join_all(futures).await?;

                eprintln!(
                    "Firing off multimanifest requests took {:?}",
                    download_timer.elapsed()
                );
            }
            _ => unreachable!(),
        },
        ("download", Some(sub_m)) => {
            let outdir = sub_m
                .value_of("outdir")
                .expect("outdir should be a required argument");
            let image = sub_m
                .value_of("image")
                .expect("image should be a required argument");
            let skip_validate = sub_m.is_present("skip-validate");

            let out_dir = Path::new(outdir);
            if !out_dir.exists() {
                return Err(failure::err_msg("outdir does not exist"));
            }

            // parse image reference
            let image = image.parse::<Reference>()?;
            eprintln!("canonical: {:#?}", image);

            // setup client
            let client = Client::new(transport_scheme, image.registry(), credentials)?;

            let download_timer = Instant::now();

            // fetch manifest
            let manifest_blob = client.get_raw_manifest(&image).await?;
            eprintln!("downloading manifest.json...");
            let mut manifest_descriptor = manifest_blob.descriptor().clone();
            let manifest_json = manifest_blob.bytes().await?;
            eprintln!("downloaded manifest.json");

            // validate manifest
            if !manifest_descriptor.digest.validate(&manifest_json) {
                return Err(failure::err_msg("manifest.json could not be validated"));
            } else {
                eprintln!("manifest.json validated");
            }

            // parse and validate the syntax of the manifest.json file
            let manifest = serde_json::from_slice::<ociv1::Manifest>(&manifest_json)
                .context("while parsing manifest.json")?;

            // annotate the manifest_descriptor with some useful metadata prior to passing
            // it off to ImageLayout. This metadata enables containerd to properly import
            // the image into it's content store / image database.
            manifest_descriptor
                .add_annotation(ociv1::annotations::key::REFNAME, &image.to_string());

            // Calculate the OCI Image Layout for this single-manifest image.
            let image_layout = ociv1::util::ImageLayout::builder()
                .manifest(manifest, manifest_descriptor.clone())
                .build();

            // create an output directory based on the image's reference name
            let out_dir = out_dir.join(image.to_string().replace(':', "-").replace('/', "_"));
            fs::create_dir(&out_dir)
                .await
                .context(format!("{:?}", out_dir))
                .context("failed to create directory")?;

            // output the oci-layout and index.json files to disk
            let (oci_layout_path, oci_layout_data) = image_layout.oci_layout();
            let (index_json_path, index_json_data) = image_layout.index_json();
            let oci_layout_data = serde_json::to_string(oci_layout_data)?;
            let index_json_data = serde_json::to_string(index_json_data)?;
            fs::write(out_dir.join(oci_layout_path), oci_layout_data).await?;
            fs::write(out_dir.join(index_json_path), index_json_data).await?;

            eprintln!("firing off download requests...");

            // build up a list of blobs to download.
            let mut paths = Vec::new();
            let mut descriptors = Vec::new();

            for (path, descriptor) in image_layout.blobs() {
                // ensure the directory exists
                fs::create_dir_all(out_dir.join(path).parent().unwrap()).await?;

                // There's some special handling around the Manifest. Although it's stored as
                // "just another blob" in the OCI layout spec, it's not actually a blob which
                // can be fetched from the /blobs/ endpoint.
                // Also, we already have it downloaded, so why re-download it?
                if descriptor == &manifest_descriptor {
                    // just dump the manifest to disk immediately
                    fs::write(out_dir.join(path), &manifest_json).await?;
                    continue;
                }

                paths.push(out_dir.join(path));
                descriptors.push(descriptor);
            }

            // fire off downloads in parallel
            let blob_futures = descriptors
                .iter()
                .map(|descriptor| client.get_blob(image.repo(), descriptor));

            // TODO: there's no need to artificially wait for all the futures to kick off.
            // This checkpoint is only here for benchmarking how well the auth header cache
            // performs.
            //
            // For maximum throughput, these futures should all immediately chain with
            // write_blob_to_file.
            let blobs = future::try_join_all(blob_futures).await?;
            eprintln!(
                "fired off all layer download requests in {:?}",
                download_timer.elapsed()
            );

            // asynchronously download and dump the blobs to disk
            let download_progress = MultiProgress::new();

            let downloads = blobs
                .into_iter()
                .zip(paths.iter())
                .map(|(blob, path)| {
                    let layer_progress = download_progress.add(ProgressBar::new(0));
                    layer_progress.set_message(&{
                        let mut msg = path
                            .file_name()
                            .unwrap()
                            .to_os_string()
                            .into_string()
                            .unwrap();
                        msg.truncate(16);
                        msg
                    });
                    layer_progress.set_style(PB_STYLE.clone());
                    layer_progress.set_length(blob.len().unwrap_or(0));

                    write_blob_to_file(&path, blob, layer_progress)
                })
                .collect::<Vec<_>>();

            let progress_handle = std::thread::spawn(move || {
                let _ = download_progress.join();
            });

            future::try_join_all(downloads).await?;
            let _ = progress_handle.join();

            eprintln!("full download flow time: {:?}", download_timer.elapsed());

            if skip_validate {
                return Ok(());
            }

            eprintln!("validating files...");

            let validation_progress = MultiProgress::new();
            let validate_progress = validation_progress.add(ProgressBar::new(paths.len() as u64));
            validate_progress.set_style(
                ProgressStyle::default_bar()
                    .template("[{elapsed_precise}] {pos}/{len} files validated"),
            );
            validate_progress.enable_steady_tick(1000);

            let validations = paths
                .iter()
                .zip(descriptors.iter())
                .map(|(path, descriptor)| validate_file(path, descriptor, &validate_progress))
                .collect::<Vec<_>>();

            let progress_handle = std::thread::spawn(move || {
                let _ = validation_progress.join();
            });

            future::try_join_all(validations).await?;
            validate_progress.finish();
            let _ = progress_handle.join();
            eprintln!("all files validated correctly");
        }
        _ => unreachable!(),
    }

    Ok(())
}

/// Writes a blob to disk as it's downloaded
async fn write_blob_to_file(
    file_path: &Path,
    mut blob: Blob,
    progress: ProgressBar,
) -> Result<(), failure::Error> {
    let mut file = File::create(&file_path)
        .await
        .context(format!("could not create {:?}", file_path))?;

    while let Some(data) = blob
        .chunk()
        .await
        .context(format!("error while downloading {:?}", file_path))?
    {
        let bytes_written = file
            .write(data.as_ref())
            .await
            .context(format!("error while writing to {:?}", file_path))?;
        progress.inc(bytes_written as u64);
    }
    progress.finish();
    Ok(())
}

/// Reads a file from disk, and validates it with the given digest
async fn validate_file(
    file_path: &Path,
    descriptor: &ociv1::Descriptor,
    progress: &ProgressBar,
) -> Result<(), failure::Error> {
    let mut validator = descriptor
        .digest
        .validator()
        .ok_or_else(|| failure::err_msg("unsupported digest algorithm"))?;

    let mut file = File::open(&file_path)
        .await
        .context(format!("could not open {:?}", file_path))?;

    let mut buf = [0; 2048];
    loop {
        let len = file.read(&mut buf).await?;
        if len == 0 {
            progress.inc(1);
            if validator.validate() {
                return Ok(());
            } else {
                return Err(failure::err_msg(format!(
                    "Digest mismatch! {:?}",
                    file_path.file_name().unwrap()
                )));
            }
        }
        validator.input(&buf[..len]);
    }
}
