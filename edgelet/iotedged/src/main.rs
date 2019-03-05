// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

#[cfg(not(target_os = "windows"))]
fn main() {
    if let Err(e) = iotedged::unix::run() {
        iotedged::logging::log_error(&e);
        std::process::exit(1);
    }
}

#[cfg(target_os = "windows")]
fn main() {
    if let Err(e) = iotedged::windows::run() {
        iotedged::logging::log_error(&e);
        std::process::exit(1);
    }
}
