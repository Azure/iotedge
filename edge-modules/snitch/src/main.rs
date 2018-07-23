// Copyright (c) Microsoft. All rights reserved.

// #[deny(warnings)]

extern crate azure_sdk_for_rust;
extern crate chrono;
extern crate futures;
extern crate http;
extern crate hyper;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate serde_yaml;
extern crate tokio;
extern crate tokio_uds;
extern crate url;
extern crate url_serde;

mod error;
mod settings;

use std::time::Instant;

use futures::future::{self, Either};
use futures::{Future, Stream};
use tokio::timer::{Delay, Interval};

use error::{Error, Result};
use settings::Settings;

fn main() -> Result<()> {
    let settings = Settings::default().merge_env()?;

    // schedule execution of the test reporter
    let reports = schedule_reports(&settings).map_err(|err| eprintln!("Report error: {:?}", err));

    tokio::run(reports.map(|_| println!("All done.")));

    Ok(())
}

fn schedule_reports(settings: &Settings) -> impl Future<Item = (), Error = Error> {
    // we schedule one report at the end of the test run
    let settings_copy = settings.clone();
    let last_report = Delay::new(Instant::now() + *settings.test_duration())
        .map_err(Error::from)
        .and_then(|_| do_report(settings_copy));

    // and we schedule another periodic one for the specified reporting interval
    let periodic_report = if let Some(reporting_interval) = settings.reporting_interval() {
        let settings_copy = settings.clone();
        Either::A(
            Interval::new(Instant::now() + *reporting_interval, *reporting_interval)
                .map_err(Error::from)
                .and_then(move |_| do_report(settings_copy.clone()))
                .collect()
                .map(|_| ()),
        )
    } else {
        Either::B(future::ok::<(), Error>(()))
    };

    last_report.join(periodic_report).map(|_| ())
}

fn do_report(_settings: Settings) -> impl Future<Item = (), Error = Error> {
    futures::future::ok(())
}
