// Copyright (c) Microsoft. All rights reserved.

use anyhow::Context;
use clap::{crate_authors, crate_description, crate_name, App};
use log::info;

#[cfg(feature = "runtime-docker")]
use edgelet_docker::Settings;

use crate::error::{Error, InitializeErrorReason};
use crate::logging;

fn create_app() -> App<'static, 'static> {
    let app = App::new(crate_name!())
        .version(edgelet_core::version_with_source_version())
        .author(crate_authors!("\n"))
        .about(crate_description!());
    app
}

pub fn init() -> anyhow::Result<Settings> {
    // Handle `--help` and `--version`
    let _ = create_app().get_matches();

    logging::init();

    info!("Starting Azure IoT Edge Module Runtime");
    info!("Version - {}", edgelet_core::version_with_source_version());

    let settings = edgelet_docker::Settings::new()
        .context(Error::Initialize(InitializeErrorReason::LoadSettings))?;
    Ok(settings)
}
