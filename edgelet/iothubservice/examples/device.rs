// Copyright (c) Microsoft. All rights reserved.

//! Sample CLI app showing how one can use this crate to access the IoT Hub
//! service API. You can run the commands supported by this CLI like so:
//!
//! ```ignore
//! $ sas_token=`iothub-explorer sas-token edgy1 | grep SharedAccessSignature | cut -d ' ' -f 2`
//! $ cargo run --example device -- -h HUB_NAME -d DEVICE_ID -s "$sas_token" get -m MODULE_ID
//! ```

extern crate chrono;
extern crate clap;
extern crate hyper;
extern crate hyper_tls;
extern crate serde_json;
extern crate tokio_core;
extern crate url;

extern crate edgelet_http;
extern crate iothubservice;

use chrono::{DateTime, Utc};
use clap::{App, Arg, ArgMatches, SubCommand};
use hyper::client::Service;
use hyper::{Client as HyperClient, Error as HyperError, Request, Response};
use hyper_tls::HttpsConnector;
use tokio_core::reactor::Core;
use url::Url;

use edgelet_http::client::{Client, TokenSource};
use edgelet_http::error::Error as HttpError;
use iothubservice::error::Error;
use iothubservice::DeviceClient;

#[derive(Clone)]
struct StaticTokenSource {
    token: String,
}

impl StaticTokenSource {
    pub fn new(token: String) -> Self {
        StaticTokenSource { token }
    }
}

impl TokenSource for StaticTokenSource {
    type Error = Error;
    fn get(&self, _expiry: &DateTime<Utc>) -> Result<String, Error> {
        Ok(self.token.clone())
    }
}

fn main() {
    let matches = parse_args();

    let sas_token = matches.value_of("sas-token").unwrap();
    let hub_name = matches.value_of("hub-name").unwrap();
    let device_id = matches.value_of("device-id").unwrap();

    let mut core = Core::new().unwrap();
    let hyper_client = HyperClient::configure()
        .connector(HttpsConnector::new(4, &core.handle()).unwrap())
        .build(&core.handle());

    let token_source = StaticTokenSource::new(sas_token.to_string());

    let client = Client::new(
        hyper_client,
        Some(token_source),
        "2017-11-08-preview",
        Url::parse(&format!("https://{}.azure-devices.net", hub_name)).unwrap(),
    ).unwrap();

    let device_client = DeviceClient::new(client, device_id).unwrap();

    if let Some(_) = matches.subcommand_matches("list") {
        list_modules(&mut core, device_client);
    } else if let Some(create) = matches.subcommand_matches("create") {
        let module_id = create.value_of("module-id").unwrap();
        create_module(&mut core, device_client, module_id);
    } else if let Some(delete) = matches.subcommand_matches("delete") {
        let module_id = delete.value_of("module-id").unwrap();
        delete_module(&mut core, device_client, module_id);
    } else if let Some(get) = matches.subcommand_matches("get") {
        let module_id = get.value_of("module-id").unwrap();
        get_module(&mut core, device_client, module_id);
    }
}

fn list_modules<S, T>(core: &mut Core, device_client: DeviceClient<S, T>)
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: 'static + TokenSource + Clone,
    T::Error: Into<HttpError>,
{
    let response = core.run(device_client.list_modules()).unwrap();
    println!("{}", serde_json::to_string_pretty(&response).unwrap());
}

fn get_module<S, T>(core: &mut Core, device_client: DeviceClient<S, T>, module_id: &str)
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: 'static + TokenSource + Clone,
    T::Error: Into<HttpError>,
{
    let response = core.run(device_client.get_module_by_id(module_id)).unwrap();
    println!("{}", serde_json::to_string_pretty(&response).unwrap());
}

fn create_module<S, T>(core: &mut Core, device_client: DeviceClient<S, T>, module_id: &str)
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: 'static + TokenSource + Clone,
    T::Error: Into<HttpError>,
{
    let response = core.run(device_client.create_module(module_id, None, None))
        .unwrap();
    println!("{}", serde_json::to_string_pretty(&response).unwrap());
}

fn delete_module<S, T>(core: &mut Core, device_client: DeviceClient<S, T>, module_id: &str)
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: 'static + TokenSource + Clone,
    T::Error: Into<HttpError>,
{
    core.run(device_client.delete_module(module_id)).unwrap();
    println!("Module {} deleted", module_id);
}

fn parse_args<'a>() -> ArgMatches<'a> {
    let module_id = Arg::with_name("module-id")
        .short("m")
        .long("module-id")
        .value_name("MODULE_ID")
        .help("Module ID")
        .required(true)
        .takes_value(true);

    App::new("List/get/create/delete module example")
        .version(env!("CARGO_PKG_VERSION"))
        .author(env!("CARGO_PKG_AUTHORS"))
        .about("Example showing how to list/create/delete modules")
        .arg(
            Arg::with_name("sas-token")
                .short("s")
                .long("sas-token")
                .value_name("SAS_TOKEN")
                .help("SAS token to use when connecting to IoT Hub")
                .required(true)
                .takes_value(true),
        )
        .arg(
            Arg::with_name("hub-name")
                .short("h")
                .long("hub-name")
                .value_name("HUB_NAME")
                .help("IoT Hub name")
                .required(true)
                .takes_value(true),
        )
        .arg(
            Arg::with_name("device-id")
                .short("d")
                .long("device-id")
                .value_name("DEVICE_ID")
                .help("Device ID")
                .required(true)
                .takes_value(true),
        )
        .subcommand(
            SubCommand::with_name("create")
                .about("Create a new module")
                .arg(module_id.clone()),
        )
        .subcommand(
            SubCommand::with_name("delete")
                .about("Delete module")
                .arg(module_id.clone()),
        )
        .subcommand(SubCommand::with_name("list").about("List modules"))
        .subcommand(
            SubCommand::with_name("get")
                .about("Get an existing module")
                .arg(module_id.clone()),
        )
        .get_matches()
}
