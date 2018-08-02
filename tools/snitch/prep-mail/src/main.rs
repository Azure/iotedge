// Copyright (c) Microsoft. All rights reserved.

extern crate env_logger;
extern crate handlebars;
extern crate serde;
extern crate serde_json;
extern crate snitcher;

use std::env;

use handlebars::Handlebars;

mod error;

use error::{Error, Result};
use snitcher::report::Report;

const REPORT_JSON_KEY: &str = "REPORT_JSON";
const REPORT_TEMPLATE_KEY: &str = "REPORT_TEMPLATE";
const DEFAULT_REPORT_TEMPLATE: &str = include_str!("mail-template.hbs");

fn main() -> Result<()> {
    env_logger::init();

    // read the report JSON from the environment
    let report_json = get_env(REPORT_JSON_KEY)?;
    let report: Report = serde_json::from_str(&report_json)?;
    let template =
        get_env(REPORT_TEMPLATE_KEY).unwrap_or_else(|_| String::from(DEFAULT_REPORT_TEMPLATE));

    // render the template and generate report
    let reg = Handlebars::new();
    println!("{}", reg.render_template(&template, &report)?);

    Ok(())
}

fn get_env(key: &str) -> Result<String> {
    env::var(key).map_err(|_| Error::Env(key.to_string()))
}
