// Copyright (c) Microsoft. All rights reserved.

extern crate clap;
extern crate env_logger;
#[macro_use]
extern crate failure;
extern crate edgelet_utils;
extern crate git2;
extern crate hex;
#[macro_use]
extern crate log;

use clap::{App, Arg};

mod error;
mod logging;
mod tree;
use logging::init_logger;
use tree::Git2Tree;

use std::env;
use std::path::Path;

fn run_check(starting_path: &Path) -> Result<(), error::Error> {
    // Create a submodule tree.
    let tree = Git2Tree::new(starting_path)?;
    let count = tree.count_flagged();
    // display the tree.
    println!("{}", tree);
    match count {
        0 => Ok(()),
        _ => Err(count)?,
    }
}

fn main() {
    init_logger();
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
        logging::log_error(&e);
        std::process::exit(1);
    } else {
        info!("No inconsistencies detected");
    }
}
