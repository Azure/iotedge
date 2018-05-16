// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::path::Path;
use std::process::Command;

//Skip ARM(cross-compile) until I figure out how to run ctest on this.
#[cfg(not(target_arch = "arm"))]
#[test]
fn run_ctest() {
    // Run iot-hsm-c tests
    println!("Start Running ctest for HSM library");
    let build_dir = Path::new(&env::var("OUT_DIR").expect("Did not find OUT_DIR in build environment"))
        .join("build");
    let test_output = Command::new("ctest")
        .arg("-C")
        .arg("Release")
        .arg("-VV")
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
