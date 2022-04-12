// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

mod error;
mod logging;
mod tree;

use std::env;
use std::path::Path;

use anyhow::Context;
use clap::{App, Arg};
use log::info;

fn run_check(starting_path: &Path) -> anyhow::Result<()> {
    // Create a submodule tree.
    let tree = tree::Git2Tree::new(starting_path).context(error::Error::Git)?;
    let count = tree.count_flagged();
    // display the tree.
    println!("{}", tree);
    match count {
        0 => Ok(()),
        _ => Err(error::Error::Count(count).into()),
    }
}

fn main() {
    logging::init_logger();
    let matches = App::new("Check Submodules")
        .about("Check all submodule reference the same commit")
        .arg(Arg::with_name("path").help("Path to git module"))
        .get_matches();

    let cwd = env::current_dir().expect("No Current Working Directory");
    let starting_path = match matches.value_of("path") {
        Some(path) => Path::new(path),
        None => Path::new(&cwd),
    };
    info!("Starting check with path {:?}", starting_path);

    if let Err(e) = run_check(starting_path) {
        logging::log_error(e.as_ref());
        std::process::exit(1);
    } else {
        info!("No inconsistencies detected");
    }
}
