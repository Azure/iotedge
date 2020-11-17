// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

fn main() {
    if let Err(e) = aziot_edge::unix::run() {
        aziot_edge::logging::log_error(&e);
        std::process::exit(i32::from(e.kind()));
    }
}
