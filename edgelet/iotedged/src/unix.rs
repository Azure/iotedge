// Copyright (c) Microsoft. All rights reserved.

use app;
use error::Error;
use signal;

pub fn run() -> Result<(), Error> {
    let settings = app::init()?;
    let main = super::Main::new(settings);

    let shutdown_signal = signal::shutdown();
    main.run_until(shutdown_signal)?;
    Ok(())
}
