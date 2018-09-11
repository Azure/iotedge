// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate env_logger;
extern crate futures;
extern crate handlebars;
extern crate hyper;
extern crate hyper_tls;
extern crate serde;
extern crate serde_json;
extern crate snitcher;
extern crate tokio;
extern crate url;

use std::env;
use std::sync::{Arc, Mutex};

use futures::future::{self, Either};
use futures::Future;
use handlebars::Handlebars;
use hyper::{Client as HyperClient, Method};
use hyper_tls::HttpsConnector;
use snitcher::client;
use snitcher::connect::HyperClientService;
use snitcher::report::Report;
use url::Url;

mod error;

use error::{Error, Result};

const REPORT_JSON_URL_KEY: &str = "REPORT_JSON_URL";
const REPORT_TEMPLATE_KEY: &str = "REPORT_TEMPLATE";
const DEFAULT_REPORT_TEMPLATE: &str = include_str!("mail-template.hbs");

fn main() -> Result<()> {
    env_logger::init();

    // read the report JSON from the environment
    let report_url = Url::parse(&get_env(REPORT_JSON_URL_KEY)?)?;

    let task = get_report_json(report_url)
        .and_then(|report_json| report_json.ok_or(Error::NoReportJsonFound))
        .and_then(|report_json| serde_json::from_str(&report_json).map_err(Error::from))
        .and_then(|report: Report| {
            let template = get_env(REPORT_TEMPLATE_KEY)
                .unwrap_or_else(|_| String::from(DEFAULT_REPORT_TEMPLATE));

            // render the template and generate report
            let reg = Handlebars::new();
            reg.render_template(&template, &report).map_err(Error::from)
        })
        .map(|html| println!("{}", html));

    let error = Arc::new(Mutex::new(None));
    let error_copy = error.clone();
    tokio::run(task.map_err(move |err| {
        *error_copy.lock().unwrap() = Some(err);
    }));

    let lock = Arc::try_unwrap(error).expect("Error lock still has multiple owners.");
    let error = lock.into_inner().expect("Error mutex cannot be locked.");

    // we want to propagate any errors we might have encountered from 'main'
    // because we want to exit with a non-zero error code when something goes
    // wrong
    Ok(error.map(|err| Err(err)).unwrap_or_else(|| Ok(()))?)
}

fn get_env(key: &str) -> Result<String> {
    env::var(key).map_err(|_| Error::Env(key.to_string()))
}

fn get_report_json(report_url: Url) -> impl Future<Item = Option<String>, Error = Error> + Send {
    HttpsConnector::new(4)
        .map(|connector| {
            let path = report_url.path().to_owned();
            let client = client::Client::new(
                HyperClientService::new(HyperClient::builder().build(connector)),
                report_url,
            );

            Either::A(
                client
                    .request_str::<()>(Method::GET, &path, None, None, false)
                    .map_err(Error::from),
            )
        })
        .map_err(Error::from)
        .unwrap_or_else(|err| Either::B(future::err(err)))
}
