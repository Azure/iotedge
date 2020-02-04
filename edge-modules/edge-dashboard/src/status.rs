// Copyright (c) Microsoft. All rights reserved.

use std::net::TcpStream;
use std::process::Command;

use actix_web::error::ErrorInternalServerError;
use actix_web::Error as ActixError;
use actix_web::*;

use crate::state::{return_response, Device};

pub fn get_state(device: web::Data<Option<Device>>) -> HttpResponse {
    if let Some(dev) = device.get_ref() {
        return_response(&dev)
    } else {
        HttpResponse::UnprocessableEntity().body("Device connection string unable to be processed.")
    }
}

pub fn get_connectivity(_req: HttpRequest, device: web::Data<Option<Device>>) -> HttpResponse {
    if let Some(dev) = device.get_ref() {
        if let Some(iothub) = dev.hub_name() {
            let iothub_hostname = &format!("{}.azure-devices.net", iothub);

            let r = resolve_and_tls_handshake(&(&**iothub_hostname, 443), iothub_hostname);
            match r {
                Ok(_) => HttpResponse::Ok().body("Succesfully connected to IoT Hub."),
                Err(_) => HttpResponse::UnprocessableEntity()
                    .body("Failed to establish connection with IoT Hub."),
            }
        } else {
            HttpResponse::UnprocessableEntity().body("IoT Hub name could not be processed")
        }
    } else {
        HttpResponse::UnprocessableEntity().body("IoT Hub name could not be processed")
    }
}

// taken from edgelet/iotedge
fn resolve_and_tls_handshake(
    to_socket_addrs: &impl std::net::ToSocketAddrs,
    tls_hostname: &str,
) -> Result<(), ActixError> {
    let host_addr = to_socket_addrs
        .to_socket_addrs()
        .map_err(ErrorInternalServerError)?
        .next()
        .ok_or_else(|| "")
        .map_err(ErrorInternalServerError)?;

    let stream = TcpStream::connect_timeout(&host_addr, std::time::Duration::from_secs(10))
        .map_err(ErrorInternalServerError)?;

    let tls_connector = native_tls::TlsConnector::new().map_err(ErrorInternalServerError)?;

    let _ = tls_connector
        .connect(tls_hostname, stream)
        .map_err(ErrorInternalServerError)?;

    Ok(())
}

pub fn get_diagnostics() -> HttpResponse {
    Command::new("iotedge")
        .args(&["check", "--output", "json"])
        .output()
        .map(|out| HttpResponse::Ok().body(out.stdout))
        .unwrap_or_else(|_| HttpResponse::ServiceUnavailable().body("Failed to execute command"))
}
