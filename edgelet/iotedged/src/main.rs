// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate iotedged;

use iotedged::Error;

fn main() {
    if let Err(e) = run() {
        iotedged::logging::log_error(&e);
        std::process::exit(1);
    }
}

fn run() -> Result<(), Error> {
    let settings = iotedged::app::init()?;
    let main = iotedged::Main::new(settings)?;

    let shutdown_signal = iotedged::signal::shutdown(&main.handle());
    main.run_until(shutdown_signal)?;
    Ok(())
}
