// Copyright (c) Microsoft. All rights reserved.

use iotedge_proxy::{app, logging, signal, Error, Routine};

fn main() {
    if let Err(e) = run() {
        logging::failure(&e);
        std::process::exit(1)
    }
}

fn run() -> Result<(), Error> {
    let settings = app::init()?;

    let main = Routine::new(settings);
    main.run_until(signal::shutdown())?;

    Ok(())
}
