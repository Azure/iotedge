// Copyright (c) Microsoft. All rights reserved.

use structopt::StructOpt;

#[derive(StructOpt)]
pub struct Settings {
    #[structopt(short = "h", long = "host", default_value = "127.0.0.1")]
    pub host: String,

    #[structopt(short = "p", long = "port", default_value = "8088")]
    pub port: String,

    #[structopt(short = "c", long = "config-path")]
    pub config_path: Option<String>,
}
