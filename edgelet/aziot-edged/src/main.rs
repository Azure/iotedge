// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

fn main() {
    log::info!("HELLO, WORLD!");
    if let Err(e) = aziot_edged::unix::run() {
        if let aziot_edged::error::ErrorKind::DeviceDeprovisioned = e.kind() {
            log::info!("Device provisioning has changed. Restarting Edge daemon to get new provisioning info.");
        } else {
            aziot_edged::logging::log_error(&e);
        }

        std::process::exit(i32::from(e.kind()));
    }
}
