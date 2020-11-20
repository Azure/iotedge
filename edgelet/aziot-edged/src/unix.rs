// Copyright (c) Microsoft. All rights reserved.

use crate::app;
use crate::error::Error;
use crate::signal;

#[cfg(feature = "runtime-docker")]
type ModuleRuntime = edgelet_docker::DockerModuleRuntime;

pub fn run() -> Result<(), Error> {
    let settings = app::init()?;
    let main = super::Main::<ModuleRuntime>::new(settings);

    main.run_until(signal::shutdown)?;
    Ok(())
}
