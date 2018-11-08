// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

extern crate iotedged;

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
