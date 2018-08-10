// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate backtrace;
extern crate env_logger;
extern crate futures;
#[macro_use]
extern crate log;
extern crate openssl_probe;
extern crate snitcher;
extern crate tokio;

use std::sync::{Arc, Mutex};

use futures::Future;

use snitcher::error::Result;
use snitcher::schedule_reports;
use snitcher::settings::Settings;

fn main() -> Result<()> {
    env_logger::init();

    // help openssl find where the certs are stored on this system
    info!("Initializing SSL cert locations.");
    openssl_probe::init_ssl_cert_env_vars();

    let settings = Settings::default().merge_env()?;

    // schedule execution of the test reporter
    let reports = schedule_reports(&settings);

    info!("Starting snitcher schedule.");

    let error = Arc::new(Mutex::new(None));
    let error_copy = error.clone();
    tokio::run(
        reports
            .map(|_| info!("All report schedules complete."))
            .map_err(move |err| {
                error!("Snitcher error: {:?}", err);
                *error_copy.lock().unwrap() = Some(err);
            }),
    );

    info!("Exiting snitch.");

    let lock = Arc::try_unwrap(error).expect("Error lock still has multiple owners.");
    let error = lock.into_inner().expect("Error mutex cannot be locked.");

    // we want to propagate any errors we might have encountered from 'main'
    // because we want to exit with a non-zero error code when something goes
    // wrong
    Ok(error.map(|err| Err(err)).unwrap_or_else(|| Ok(()))?)
}
