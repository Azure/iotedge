// Copyright (c) Microsoft. All rights reserved.

use std::ffi::OsStr;

use lazy_static::lazy_static;

use aziotctl_common::system::{
    get_status, get_system_logs as logs, restart, set_log_level as log_level, stop,
    ServiceDefinition, SERVICE_DEFINITIONS as IS_SERVICES,
};

use aziot_identity_client_async::Client as IdentityClient;
use aziot_identity_common_http::ApiVersion;

use crate::error::{Error, ErrorKind};

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
    pub fn get_system_logs(args: &[&OsStr]) -> Result<(), Error> {
        let services: Vec<&str> = SERVICE_DEFINITIONS.iter().map(|s| s.service).collect();

        logs(&services, args).map_err(|err| {
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

    pub fn system_stop() -> Result<(), Error> {
        stop(&SERVICE_DEFINITIONS).map_err(|err| {
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

    pub async fn reprovision() -> Result<(), Error> {
        let uri = url::Url::parse("unix:///run/aziot/identityd.sock")
            .expect("hard-coded URI should parse");
        let client = IdentityClient::new(
            ApiVersion::V2020_09_01,
            http_common::Connector::new(&uri).map_err(|err| {
                eprintln!("Failed to make identity client: {}", err);
                Error::from(ErrorKind::System)
            })?,
            1,
        );

        client.reprovision().await.map_err(|err| {
            eprintln!("Failed to reprovision: {}", err);
            Error::from(ErrorKind::System)
        })?;

        println!("Successfully reprovisioned with IoT Hub.");

        restart(&[&IOTEDGED]).map_err(|err| {
            eprintln!("{:#?}", err);
            Error::from(ErrorKind::System)
        })?;

        Ok(())
    }
}
