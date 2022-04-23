// Copyright (c) Microsoft. All rights reserved.

use std::ffi::OsStr;

use anyhow::Context;
use lazy_static::lazy_static;

use aziotctl_common::system::{
    get_status, get_system_logs as logs, restart, set_log_level as log_level, stop,
    ServiceDefinition, SERVICE_DEFINITIONS as IS_SERVICES,
};

use aziot_identity_common_http::ApiVersion;
use identity_client::IdentityClient;

use crate::error::Error;

lazy_static! {
    static ref IOTEDGED: ServiceDefinition = {
        // If IOTEDGE_LISTEN_MANAGEMENT_URI isn't set at compile-time, assume socket activation is being used.
        //
        // This doesn't matter for released packages since those always have IOTEDGE_LISTEN_MANAGEMENT_URI set.
        // It's only useful for non-package builds.
        let uses_socket_activation = option_env!("IOTEDGE_LISTEN_MANAGEMENT_URI").map_or(true, |value| value.starts_with("fd://"));

        let sockets: &'static [&'static str] =
            if uses_socket_activation {
                &["aziot-edged.mgmt.socket", "aziot-edged.workload.socket"]
            }
            else {
                &[]
            };

        ServiceDefinition {
            service: "aziot-edged.service",
            sockets,
        }
    };

    static ref SERVICE_DEFINITIONS: Vec<&'static ServiceDefinition> =
        std::iter::once(&*IOTEDGED)
            .chain(IS_SERVICES.iter().copied())
            .collect();
}

pub struct System;

impl System {
    pub fn get_system_logs(args: &[&OsStr]) -> anyhow::Result<()> {
        let services: Vec<&str> = SERVICE_DEFINITIONS.iter().map(|s| s.service).collect();

        logs(&services, &args)
            .context(Error::System)
            .map_err(|err| {
                eprintln!("{:#?}", err);
                err
            })
    }

    pub fn system_restart() -> anyhow::Result<()> {
        restart(&SERVICE_DEFINITIONS)
            .context(Error::System)
            .map_err(|err| {
                eprintln!("{:#?}", err);
                err
            })
    }

    pub fn system_stop() -> anyhow::Result<()> {
        stop(&SERVICE_DEFINITIONS)
            .context(Error::System)
            .map_err(|err| {
                eprintln!("{:#?}", err);
                err
            })
    }

    pub fn set_log_level(level: log::Level) -> anyhow::Result<()> {
        log_level(&SERVICE_DEFINITIONS, level)
            .context(Error::System)
            .map_err(|err| {
                eprintln!("{:#?}", err);
                err
            })
    }

    pub fn get_system_status() -> anyhow::Result<()> {
        get_status(&SERVICE_DEFINITIONS)
            .context(Error::System)
            .map_err(|err| {
                eprintln!("{:#?}", err);
                err
            })
    }

    pub fn reprovision(runtime: &mut tokio::runtime::Runtime) -> anyhow::Result<()> {
        let uri = url::Url::parse("unix:///run/aziot/identityd.sock")
            .expect("hard-coded URI should parse");
        let client = IdentityClient::new(ApiVersion::V2020_09_01, &uri);

        let provisioning_cache =
            std::path::Path::new("/var/lib/aziot/edged/cache/provisioning_state").to_path_buf();

        runtime
            .block_on(client.reprovision_device(provisioning_cache))
            .context(Error::System)
            .map_err(|err| {
                eprintln!("Failed to reprovision: {}", err);
                err
            })?;

        println!("Successfully reprovisioned with IoT Hub.");

        restart(&[&IOTEDGED]).context(Error::System).map_err(|err| {
            eprintln!("{:#?}", err);
            err
        })
    }
}
