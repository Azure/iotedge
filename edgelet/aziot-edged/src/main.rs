// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use aziot_edged::Error;

fn main() {
    if let Err(e) = aziot_edged::unix::run() {
        aziot_edged::logging::log_error(e.as_ref());

        std::process::exit(e.downcast_ref::<Error>().map_or(1, i32::from));
    }
}
