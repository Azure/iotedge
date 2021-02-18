// Copyright (c) Microsoft. All rights reserved.
#![allow(warnings)]
use std::ffi::OsStr;

use crate::error::{Error, ErrorKind};
use aziotctl_common::{
    get_status, get_system_logs as logs, restart, set_log_level as log_level, ServiceDefinition,
    SERVICE_DEFINITIONS as IS_SERVICES,
};

lazy_static! {
    static ref iotedged: ServiceDefinition = {
        let sockets: &'static [&'static str] = option_env!("IOTEDGE_HOST").map_or(&[], |_host| {
            &["aziot-edged.mgmt.socket", "aziot-edged.workload.socket"]
        });

        ServiceDefinition {
            service: "aziot-iotedgd.service",
            sockets,
        }
    };
    static ref SERVICE_DEFINITIONS: Vec<&'static ServiceDefinition> = {
        let iotedged_ref: &ServiceDefinition = &iotedged;

        let service_definitions: Vec<&ServiceDefinition> = std::iter::once(iotedged_ref)
            .chain(IS_SERVICES.iter().copied())
            .collect();

        service_definitions
    };
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
