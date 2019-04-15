// Copyright (c) Microsoft. All rights reserved.
#![deny(rust_2018_idioms)]
fn main() {
    #[cfg(windows)]
    windows::main()
}

#[cfg(windows)]
mod windows {
    use std::env;
    use std::fs;
    use std::io::Result;
    use std::path::{Path, PathBuf};
    use std::process::Command;

    use version_compare::VersionCompare;
    use winreg::enums::*;
    use winreg::RegKey;

    pub fn main() {
        // on windows we only support x64 at this time
        // let target_arch = env::var("CARGO_CFG_TARGET_ARCH").unwrap();
        // if target_arch != "x86_64" {
        //    panic!("Only x64 architecture is supported on Windows at this time.");
        // }

        let sdk_bin_root = get_sdk_bin_root().expect("Could not get Windows SDK root bin path");
        let mc_path = sdk_bin_root.join("mc.exe");
        let rc_path = sdk_bin_root.join("rc.exe");
        let out_dir = env::var("OUT_DIR").unwrap();

        Command::new(mc_path.to_str().unwrap())
            .args(&[
                "-r",
                &out_dir,
                "-h",
                &out_dir,
                "src\\resources\\event_messages.mc",
            ])
            .status()
            .expect("Message compilation failed");

        let rc_source_path = Path::new(&out_dir).join("event_messages.rc");
        Command::new(rc_path.to_str().unwrap())
            .args(&[rc_source_path.to_str().unwrap()])
            .status()
            .expect("Resource compilation failed");

        fs::rename(
            Path::new(&out_dir).join("event_messages.res"),
            Path::new(&out_dir).join("event_messages.res.lib"),
        )
        .expect("Rename of event_messages.res failed");

        println!("cargo:rustc-link-search=all={}", out_dir);
        println!("cargo:rustc-link-lib=event_messages.res");
    }

    fn get_sdk_bin_root() -> Result<PathBuf> {
        let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
        let installed_roots =
            hklm.open_subkey("SOFTWARE\\Microsoft\\Windows Kits\\Installed Roots")?;
        let max_version = installed_roots
            .enum_keys()
            .map(|v| v.unwrap())
            .max_by(|v1, v2| VersionCompare::compare(v1, v2).unwrap().ord().unwrap())
            .unwrap();

        let kits_root: String = installed_roots.get_value("KitsRoot10").unwrap();
        let install_root = Path::new(&kits_root);

        Ok(install_root.join("bin").join(max_version).join("x64"))
    }
}
