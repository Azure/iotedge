// Copyright (c) Microsoft. All rights reserved.

use std::ffi::OsStr;

use lazy_static::lazy_static;

use aziotctl_common::{
    get_status, get_system_logs as logs, restart, set_log_level as log_level, ServiceDefinition,
    SERVICE_DEFINITIONS as IS_SERVICES,
};

use crate::error::{Error, ErrorKind};

lazy_static! {
    static ref IOTEDGED: ServiceDefinition = {
        // If IOTEDGE_LISTEN_MANAGEMENT_URI isn't set at compile-time, assume socket activation is being used.
        //
        // This doesn't matter for release builds since those always have IOTEDGE_LISTEN_MANAGEMENT_URI set.
        // It's only useful for developers' builds.
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
    pub fn get_system_logs(args: &[&OsStr]) -> Result<(), Error> {
        let services: Vec<&str> = SERVICE_DEFINITIONS.iter().map(|s| s.service).collect();

        logs(&services, &args).map_err(|err| {
            eprintln!("{:#?}", err);
            Error::from(ErrorKind::System)
        })
    }

    pub fn system_restart() -> Result<(), Error> {
        restart(&SERVICE_DEFINITIONS).map_err(|err| {
            eprintln!("{:#?}", err);
            Error::from(ErrorKind::System)
        })
    }

    pub fn set_log_level(level: log::Level) -> Result<(), Error> {
        log_level(&SERVICE_DEFINITIONS, level).map_err(|err| {
            eprintln!("{:#?}", err);
            Error::from(ErrorKind::System)
        })
    }

    pub fn get_system_status() -> Result<(), Error> {
        get_status(&SERVICE_DEFINITIONS).map_err(|err| {
            eprintln!("{:#?}", err);
            Error::from(ErrorKind::System)
        })
    }
}
