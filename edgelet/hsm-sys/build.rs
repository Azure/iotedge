// Copyright (c) Microsoft. All rights reserved.
extern crate cmake;
extern crate git2;

use std::env;

use cmake::Config;
use git2::{Oid, Repository};

#[cfg(windows)]
const SSL_OPTION: &str = "use_schannel";

#[cfg(unix)]
const SSL_OPTION: &str = "use_openssl";

trait SetPlatformDefines {
    fn set_platform_defines(&mut self) -> &mut Self;
    fn set_build_shared(&mut self) -> &mut Self;
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
        let rv = if (env::var("PROFILE").unwrap() == "Release"
            && env::var("TARGET").unwrap().starts_with("x86_64"))
            || env::var("NO_VALGRIND").is_ok()
        {
            "OFF"
        } else {
            "ON"
        };
        self.define("run_valgrind", rv)
    }

    // The "debug_assertions" configuration flag seems to be the way to detect
    // if this is a "dev" build or any other kind of build.
    #[cfg(debug_assertions)]
    fn set_build_shared(&mut self) -> &mut Self {
        self.define("BUILD_SHARED", "OFF")
    }

    #[cfg(not(debug_assertions))]
    fn set_build_shared(&mut self) -> &mut Self {
        self.define("BUILD_SHARED", "ON")
    }
}

fn main() {
    // Clone Azure C -shared library
    let c_shared_url = "https://github.com/Azure/azure-c-shared-utility";
    let c_shared_repo = "azure-iot-hsm-c/azure-c-shared-utility";
    let version_sha = "8290634e5c2d005643d5a7dd5f8e65ad7a4353c2"; // 2018-05-03

    println!("#Start Getting C-Shared Utilities");
    let repo = Repository::open(c_shared_repo)
        .or_else(|_| Repository::clone_recurse(c_shared_url, c_shared_repo))
        .expect("C-Shared repo could not be opened.");

    let oid = Oid::from_str(version_sha).expect("Could not create a treeish oid");
    let treeish = repo.find_commit(oid)
        .or_else(|_| {
            //  Attempt a fetch if finding the commit failed.
            repo.remotes()
                .map(|remotes| {
                    for remote in remotes.iter() {
                        if let Some(remote_name) = remote {
                            println!("# Attempt fetch on remote {}", remote_name);
                            repo.find_remote(remote_name)
                                .unwrap()
                                .fetch(&["master"], None, None)
                                .expect("Could not find commit and git fetch failed");
                        }
                    }
                })
                .expect("Could not find commit, and no remotes are set.");
            // Try again after fetch
            repo.find_commit(oid)
        })
        .expect("SHA not found in C-Shared repo");

    repo.checkout_tree(treeish.as_object(), None)
        .expect("Unable to checkout SHA in C-Shared repo");

    repo.submodules()
        .unwrap()
        .into_iter()
        .for_each(|mut submodule| submodule.update(true, None).unwrap());

    println!("#Done Getting C-Shared Utilities");

    // make the C libary at azure-iot-hsm-c (currently a subdirectory in this
    // crate)
    // Always make the Release version because Rust links to the Release CRT.
    // (This is especially important for Windows)
    println!("#Start building HSM dev-mode library");
    let iothsm = Config::new("azure-iot-hsm-c")
        .define(SSL_OPTION, "ON")
        .define("CMAKE_BUILD_TYPE", "Release")
        .define("run_unittests", "ON")
        .set_platform_defines()
        .set_build_shared()
        .profile("Release")
        .build();

    println!("#Done building HSM dev-mode library");

    // where to find the library (The "link-lib" should match the library name
    // defined in the CMakefile.txt)

    println!("cargo:rerun-if-env-changed=NO_VALGRIND");
    // For libraries which will just install in target directory
    println!("cargo:rustc-link-search=native={}", iothsm.display());
    // For libraries (ie. C Shared) which will install in $target/lib
    println!("cargo:rustc-link-search=native={}/lib", iothsm.display());
    println!("cargo:rustc-link-lib=iothsm");

    // we need to explicitly link with c shared util only when we build the C
    // library as a static lib which we do only in rust debug builds
    #[cfg(debug_assertions)]
    println!("cargo:rustc-link-lib=aziotsharedutil");
}
