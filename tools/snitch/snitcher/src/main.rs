// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate futures;
extern crate openssl_probe;
extern crate snitcher;
extern crate tokio;

use futures::Future;

use snitcher::error::Result;
use snitcher::schedule_reports;
use snitcher::settings::Settings;

fn main() -> Result<()> {
    // help openssl find where the certs are stored on this system
    openssl_probe::init_ssl_cert_env_vars();

    let settings = Settings::default().merge_env()?;

    // schedule execution of the test reporter
    let reports = schedule_reports(&settings).map_err(|err| eprintln!("Report error: {:?}", err));

    tokio::run(reports.map(|_| println!("All done.")));

    Ok(())
}
