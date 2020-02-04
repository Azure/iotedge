// Copyright (c) Microsoft. All rights reserved.

use structopt::StructOpt;

#[derive(StructOpt)]
pub struct Settings {
    #[structopt(short = "h", long = "host")]
    pub host: String,

    #[structopt(short = "p", long = "port")]
    pub port: String,

    #[structopt(short = "c", long = "config-path")]
    pub config_path: Option<String>,
}
