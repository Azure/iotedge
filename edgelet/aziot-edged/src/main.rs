// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

fn main() {
    if let Err(e) = aziot_edged::unix::run() {
        let inner = e.downcast_ref();

        if let Some(aziot_edged::Error::DeviceDeprovisioned) = &inner {
            log::info!("Device provisioning has changed. Restarting Edge daemon to get new provisioning info.");
        } else {
            aziot_edged::logging::log_error(e.as_ref());
        }

        std::process::exit(inner.map_or(1, i32::from));
    }
}
