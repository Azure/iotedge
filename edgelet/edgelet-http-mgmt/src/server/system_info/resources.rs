// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::future::IntoFuture;
use futures::Future;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use serde::Serialize;
use serde_json;
use sysinfo::{DiskExt, SystemExt};

use edgelet_core::{Module, ModuleRuntime, RuntimeOperation};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct GetSystemResources<M> {
    runtime: M,
}

impl<M> GetSystemResources<M> {
    pub fn new(runtime: M) -> Self {
        GetSystemResources { runtime }
    }
}

impl<M> Handler<Parameters> for GetSystemResources<M>
where
    M: 'static + ModuleRuntime + Send,
    <M::Module as Module>::Config: Serialize,
{
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        debug!("Get System Resources");

        let response = self
            .runtime
            .system_info()
            .then(|_system_info| -> Result<_, Error> {
                let body = SystemResources::new();

                let b = serde_json::to_string(&body)
                    .context(ErrorKind::RuntimeOperation(RuntimeOperation::SystemInfo))?;

                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, b.len().to_string().as_str())
                    .body(b.into())
                    .context(ErrorKind::RuntimeOperation(RuntimeOperation::SystemInfo))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()));

        Box::new(response.into_future())
    }
}

// Temp

#[derive(Clone, Debug, Default, serde_derive::Serialize)]
struct SystemResources {
    used_ram: u64,
    total_ram: u64,

    disks: Vec<DiskInfo>,
}

impl SystemResources {
    fn new() -> Self {
        // #[cfg(unix)]
        {
            let mut system = sysinfo::System::new();
            system.refresh_all();
            SystemResources {
                total_ram: system.get_total_memory(),
                used_ram: system.get_used_memory(),

                disks: system.get_disks().iter().map(DiskInfo::new).collect(),
            }
        }

        // #[cfg(not(unix))]
        // return SystemResources::default();
    }
}

#[derive(Clone, Debug, Default, serde_derive::Serialize)]
struct DiskInfo {
    name: String,
    available_space: u64,
    total_space: u64,
    file_system: String,
    file_type: String,
}

// #[cfg(unix)]
impl DiskInfo {
    fn new<T>(disk: &T) -> Self
    where
        T: DiskExt,
    {
        let available_space = disk.get_available_space();
        let total_space = disk.get_total_space();
        #[allow(clippy::cast_precision_loss)]
        DiskInfo {
            name: disk.get_name().to_string_lossy().into_owned(),
            available_space,
            total_space,
            file_system: String::from_utf8_lossy(disk.get_file_system()).into_owned(),
            file_type: format!("{:?}", disk.get_type()),
        }
    }
}
