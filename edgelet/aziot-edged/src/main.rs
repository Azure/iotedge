// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms)]
#![warn(clippy::all, clippy::pedantic)]

mod management;

#[tokio::main]
async fn main() {
    let version = edgelet_core::version_with_source_version();

    clap::App::new(clap::crate_name!())
        .version(version.as_str())
        .author(clap::crate_authors!("\n"))
        .about(clap::crate_description!())
        .get_matches();

    logger::try_init()
        .expect("cannot fail to initialize global logger from the process entrypoint");

    log::info!("Starting Azure IoT Edge Module Runtime");
    log::info!("Version - {}", edgelet_core::version_with_source_version());

    if let Err(err) = run().await {
        log::error!("{}", err);
        //std::process::exit(i32::from(err.kind()));
    }
}

async fn run() -> Result<(), std::io::Error> {
    management::start().await;

    unreachable!()
}
