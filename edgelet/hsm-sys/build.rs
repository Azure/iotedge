// Copyright (c) Microsoft. All rights reserved.
extern crate cmake;

use cmake::Config;

fn main() {
    // make the C libary at azure-iot-hsm-c (currently a subdirectory in this
    // crate)
    // Always make the Release version because Rust links to the Release CRT.
    // (This is especially important for Windows)
    let iothsm = Config::new("azure-iot-hsm-c")
        .define("BUILD_SHARED_LIBS", "ON")
        .define("CMAKE_BUILD_TYPE", "Release")
        .profile("Release")
        .build();

    // where to find the library (The "link-lib" should match the library name
    // defined in the CMakefile.txt)
    println!("cargo:rustc-link-search=native={}", iothsm.display());
    println!("cargo:rustc-link-lib=iothsm");
}
