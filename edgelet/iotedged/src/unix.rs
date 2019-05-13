// Copyright (c) Microsoft. All rights reserved.
use futures::Future;

use crate::app;
use crate::signal;
use crate::error::Error;

pub fn run() -> Result<(), Error> {
    let settings = app::init()?;
    let main = super::Main::new(settings);

    main.run_until(create_shutdown_future)?;
    Ok(())
}

fn create_shutdown_future() -> Box<dyn Future<Item = (), Error = ()> + Send> {
    Box::new(signal::shutdown())
}