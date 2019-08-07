// Copyright (c) Microsoft. All rights reserved.

use edge_proxy::{app, Error, Routine};
use edge_proxy::{logging, signal};

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
