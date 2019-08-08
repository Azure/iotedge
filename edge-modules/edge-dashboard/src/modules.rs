// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use actix_web::error::ErrorInternalServerError;
use actix_web::Error as ActixError;
use actix_web::*;
use bytes::BytesMut;
use edgelet_core::{LogOptions, Module as EdgeModule, ModuleRuntime, RuntimeSettings};
use edgelet_http_mgmt::*;
use futures::future::{ok, Either, IntoFuture};
use futures::stream::Stream;
use futures::Async;
use futures::Future;
use serde::{Deserialize as JSONDeserialize, Serialize};
use url::Url;

use crate::health::Status;
use crate::AuthRequest;
use crate::Context;

#[derive(Debug, JSONDeserialize, Serialize)]
pub struct Module {
    name: String,
    status: String,
}

impl Module {
    pub fn new(name: String, status: String) -> Self {
        Module { name, status }
    }

    pub fn name(&self) -> &String {
        &self.name
    }

    pub fn status(&self) -> &String {
        &self.status
    }
}

pub fn restart_module(
    req: HttpRequest,
    context: web::Data<Arc<Context>>,
    info: web::Query<AuthRequest>,
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    let module_id = req.match_info().get("id");
    let api_ver = &info.api_version;
    let response = context
        .edge_config
        .as_ref()
        .map(|config| {
            let mgmt_uri = config.connect().management_uri();
            Either::A(
                Url::parse(&format!("{}/modules/?api-version={}", mgmt_uri, api_ver))
                    .map_err(ErrorInternalServerError)
                    .and_then(|url| ModuleClient::new(&url).map_err(ErrorInternalServerError))
                    .map(|mod_client| {
                        if let Some(id) = module_id {
                            mod_client.restart(id)
                        } else {
                            mod_client.restart("")
                        }
                        .map(|_| HttpResponse::Ok().body(""))
                        .map_err(ErrorInternalServerError)
                    })
                    .into_future()
                    .flatten(),
            )
        })
        .unwrap_or_else(|err| {
            Either::B(ok(HttpResponse::ServiceUnavailable()
                .content_type("text/plain")
                .body(format!("{:?}", err))))
        });

    Box::new(response)
}

pub fn get_logs(
    req: HttpRequest,
    context: web::Data<Arc<Context>>,
    info: web::Query<AuthRequest>,
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    let module_id = req.match_info().get("id").unwrap_or("");
    let api_ver = &info.api_version;

    let response = context
        .edge_config
        .as_ref()
        .map(move |config| {
            let mgmt_uri = config.connect().management_uri();
            Either::A(
                Url::parse(&format!("{}/modules/?api-version={}", mgmt_uri, api_ver))
                    .map_err(ErrorInternalServerError)
                    .and_then(|url| ModuleClient::new(&url).map_err(ErrorInternalServerError)) // can't connect to the endpoint
                    .map(move |mod_client| {
                        mod_client
                            .logs(module_id, &LogOptions::new())
                            .map_err(ErrorInternalServerError)
                            .and_then(|data| {
                                data.map_err(ErrorInternalServerError)
                                    .fold(BytesMut::new(), |mut acc, chunk| {
                                        let stream = chunk.as_ref();
                                        if stream.len() >= 8 {
                                            let (_, right) = stream.split_at(8);
                                            acc.extend_from_slice(right);
                                        }
                                        Ok::<_, ActixError>(acc)
                                    })
                                    .and_then(|body| {
                                        let mut clone = body.freeze().to_vec().clone();
                                        clone.retain(|&byte| (byte as char).is_ascii());
                                        if let Ok(content) = String::from_utf8(clone) {
                                            HttpResponse::Ok().body(content)
                                        } else {
                                            HttpResponse::ServiceUnavailable()
                                                .body("Logs unable to be displayed")
                                        }
                                    })
                            })
                    })
                    .into_future()
                    .flatten(),
            )
        })
        .unwrap_or_else(|err| {
            Either::B(ok(HttpResponse::ServiceUnavailable()
                .content_type("text/plain")
                .body(format!("{:?}", err))))
        });

    Box::new(response)
}

pub fn get_modules(
    context: web::Data<Arc<Context>>,
    info: web::Query<AuthRequest>,
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    return_modules(context, &info.api_version, module_response)
}

fn module_response(mods: Vec<Module>) -> HttpResponse {
    HttpResponse::Ok()
        .content_type("text/json")
        .body(format!("{:?}", mods))
}

pub fn get_health(
    context: web::Data<Arc<Context>>,
    info: web::Query<AuthRequest>,
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    return_modules(context, &info.api_version, health_response)
}

fn health_response(mods: Vec<Module>) -> HttpResponse {
    let mut device_status = Status::new();
    let mut edge_agent = false;
    let mut edge_hub = false;
    let mut other = true;

    for module in mods.iter() {
        if module.name() == "edgeAgent" {
            if module.status() == "running" {
                edge_agent = true;
            }
        } else if module.name() == "edgeHub" {
            if module.status() == "running" {
                edge_hub = true;
            }
        } else {
            if module.status() != "running" {
                other = false;
            }
        }
    }

    device_status.set_iotedged();
    device_status.set_edge_agent(edge_agent);
    device_status.set_edge_hub(edge_hub);
    device_status.set_other_modules(edge_agent && edge_hub && other);

    let health = device_status.return_health();
    HttpResponse::Ok().body(format!(
        "Device health: {:?}\nDevice details: {:?}",
        health, device_status
    ))
}

fn return_modules(
    context: web::Data<Arc<Context>>,
    api_ver: &str,
    f: fn(Vec<Module>) -> HttpResponse,
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    let response = context
        .edge_config
        .as_ref()
        .map(move |config| {
            let mgmt_uri = config.connect().management_uri();
            Either::A(
                Url::parse(&format!("{}/modules/?api-version={}", mgmt_uri, api_ver))
                    .map_err(ErrorInternalServerError)
                    .and_then(|url| ModuleClient::new(&url).map_err(ErrorInternalServerError))
                    .map(|mod_client| {
                        mod_client
                            .list()
                            .map(move |data| {
                                let mods: Vec<Module> = data
                                    .iter()
                                    .map(move |c| {
                                        let status =
                                            if let Ok(Async::Ready(t)) = c.runtime_state().poll() {
                                                (*(t.status().clone()).to_string()).to_string()
                                            } else {
                                                "".to_string()
                                            };
                                        Module::new(c.name().to_string(), status)
                                    })
                                    .collect();
                                f(mods) // changes depending on API call
                            })
                            .map_err(ErrorInternalServerError)
                    })
                    .into_future()
                    .flatten(),
            )
        })
        .unwrap_or_else(|err| {
            Either::B(ok(HttpResponse::ServiceUnavailable()
                .content_type("text/plain")
                .body(format!("{:?}", err))))
        });

    Box::new(response)
}
