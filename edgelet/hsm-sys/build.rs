// Copyright (c) Microsoft. All rights reserved.
extern crate cmake;
extern crate git2;

use cmake::Config;
use git2::{Oid, Repository};

#[cfg(windows)]
const SSL_OPTION: &str = "use_schannel";

#[cfg(unix)]
const SSL_OPTION: &str = "use_openssl";

trait SetPlatformDefines {
    fn set_platform_defines(&mut self) -> &mut Self;
}

impl SetPlatformDefines for Config {
    #[cfg(windows)]
    fn set_platform_defines(&mut self) -> &mut Self {
        // C-shared library wants Windows flags (/DWIN32 /D_WINDOWS) for Windows,
        // and the cmake library overrides this.
        self.cflag("/DWIN32")
            .cxxflag("/DWIN32")
            .cflag("/D_WINDOWS")
            .cxxflag("/D_WINDOWS")
    }

    #[cfg(unix)]
    fn set_platform_defines(&mut self) -> &mut Self {
        self
    }
}

fn main() {
    // Clone Azure C -shared library
    let c_shared_url = "https://github.com/Azure/azure-c-shared-utility";
    let c_shared_repo = "azure-iot-hsm-c/azure-c-shared-utility";
    let version_sha = "ed84cdb8f2cd345c1bebde9e57b896e96cef374c"; // 2018-04-02

    let repo = Repository::open(c_shared_repo)
        .or_else(|_| Repository::clone_recurse(c_shared_url, c_shared_repo))
        .expect("C-Shared repo could not be opened.");

    let oid = Oid::from_str(version_sha).expect("Could not create a treeish oid");
    let treeish = repo.find_commit(oid)
        .expect("SHA not found in C-Shared repo");

    repo.checkout_tree(treeish.as_object(), None)
        .expect("Unable to checkout SHA in C-Shared repo");

    repo.submodules()
        .unwrap()
        .into_iter()
        .for_each(|mut submodule| submodule.update(true, None).unwrap());

    // make the C libary at azure-iot-hsm-c (currently a subdirectory in this
    // crate)
    // Always make the Release version because Rust links to the Release CRT.
    // (This is especially important for Windows)
    let iothsm = Config::new("azure-iot-hsm-c")
        .define(SSL_OPTION, "ON")
        .define("CMAKE_BUILD_TYPE", "Release")
        .set_platform_defines()
        .profile("Release")
        .build();

    // where to find the library (The "link-lib" should match the library name
    // defined in the CMakefile.txt)

    // For libraries which will just install in target directory
    println!("cargo:rustc-link-search=native={}", iothsm.display());
    // For libraries (ie. C Shared) which will install in $target/lib
    println!("cargo:rustc-link-search=native={}/lib", iothsm.display());
    println!("cargo:rustc-link-lib=iothsm");
}
