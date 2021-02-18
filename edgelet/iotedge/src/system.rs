// Copyright (c) Microsoft. All rights reserved.
#![allow(warnings)]
use std::ffi::OsStr;

use crate::error::{Error, ErrorKind};
use aziotctl_common::{get_system_logs as logs, ServiceDefinition, SERVICE_DEFINITIONS};

pub struct System;

impl System {
    pub fn get_system_logs(args: &[&OsStr]) -> Result<(), Error> {
        let iotedged = ServiceDefinition {
            service: "aziot-iotedgd.service",
            sockets: &["aziot-identityd.socket"],
        };

        let service_definitions: Vec<&ServiceDefinition> = std::iter::once(&iotedged)
            .chain(SERVICE_DEFINITIONS.iter().map(|s| *s))
            .collect();

        let services: Vec<&str> = service_definitions.iter().map(|s| s.service).collect();

        logs(&services, &args).map_err(|err| {
            eprintln!("{:#?}", err);
            Error::from(ErrorKind::System)
        })
    }
}
