// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

extern crate num_cpus;

use std::env;
use std::path::Path;
use std::process::Command;

//Skip ARM(cross-compile) until I figure out how to run ctest on this.
#[cfg(not(any(target_arch = "arm", target_arch = "aarch64")))]
#[test]
fn run_ctest() {
    // Run iot-hsm-c tests
    println!("Start Running ctest for HSM library");
    let build_dir =
        Path::new(&env::var("OUT_DIR").expect("Did not find OUT_DIR in build environment"))
            .join("build");
    let test_output = Command::new("ctest")
        .arg("-C")
        .arg("Release")
        .arg("-VV")
        .arg(format!("-j {}", num_cpus::get()))
        .current_dir(build_dir)
        .output()
        .expect("failed to execute ctest");
    println!("status: {}", test_output.status);
    println!("stdout: {}", String::from_utf8_lossy(&test_output.stdout));
    println!("stderr: {}", String::from_utf8_lossy(&test_output.stderr));
    if !test_output.status.success() {
        panic!("Running CTest failed.");
    }
    println!("Done Running ctest for HSM library");
}
