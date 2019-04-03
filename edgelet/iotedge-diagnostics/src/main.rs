// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::use_self)]

#[macro_use]
extern crate clap;

use std::net::{TcpStream, ToSocketAddrs};

use futures::{Future, Stream};

use edgelet_core::UrlExt;

#[cfg(unix)]
use hyperlocal::{UnixConnector, Uri as UnixUri};
#[cfg(windows)]
use hyperlocal_windows::{UnixConnector, Uri as UnixUri};

fn main() -> Result<(), Error> {
    let app = app_from_crate!()
        .setting(clap::AppSettings::SubcommandRequiredElseHelp)
        .setting(clap::AppSettings::VersionlessSubcommands)
        .subcommand(
            clap::SubCommand::with_name("edge-agent")
                .about("edge-agent diagnostics")
                .arg(
                    clap::Arg::with_name("management-uri")
                        .long("management-uri")
                        .required(true)
                        .takes_value(true)
                        .help("URI of management endpoint"),
                ),
        )
        .subcommand(
            clap::SubCommand::with_name("iothub")
                .about("connect to Azure IoT Hub")
                .arg(
                    clap::Arg::with_name("hostname")
                        .long("hostname")
                        .required(true)
                        .takes_value(true)
                        .help("Hostname of Azure IoT Hub"),
                )
                .arg(
                    clap::Arg::with_name("port")
                        .long("port")
                        .required(true)
                        .takes_value(true)
                        .help("Port to connect to"),
                ),
        )
        .subcommand(clap::SubCommand::with_name("local-time").about("print local time"));

    let matches = app.get_matches();

    let mut runtime = tokio::runtime::Runtime::new()
        .map_err(|err| format!("could not create tokio runtime: {}", err))?;

    match matches.subcommand() {
        ("edge-agent", Some(matches)) => {
            let management_uri = matches
                .value_of("management-uri")
                .expect("parameter is required");
            let management_uri = url::Url::parse(management_uri)
                .map_err(|err| format!("could not parse management URI: {}", err))?;

            let list_modules_response = match management_uri.scheme() {
                "unix" => {
                    let client =
                        hyper::Client::builder().build::<_, hyper::Body>(UnixConnector::new());
                    let uri = UnixUri::new(
                        management_uri
                            .to_uds_file_path()
                            .map_err(|err| format!("couldn't get file path from URI: {}", err))?,
                        "/modules/?api-version=2018-06-28",
                    );
                    client.get(uri.into())
                }

                "http" => {
                    let client = hyper::Client::new();
                    let uri = management_uri
                        .join("/modules/?api-version=2018-06-28")
                        .map_err(|err| {
                            format!("could not construct list-modules request URI: {}", err)
                        })?;
                    client.get(uri.to_string().parse().map_err(|err| {
                        format!("could not convert list-modules request URI from url::Url to hyper::Uri: {}", err)
                    })?)
                }

                scheme => {
                    return Err(format!(
                        "unrecognized scheme {:?} in management URI {:?}",
                        scheme, management_uri
                    )
                    .into());
                }
            };

            let f = list_modules_response
                .then(|response| {
                    let response = response.map_err(|err| {
                        format!("could not execute list-modules request: {}", err)
                    })?;
                    assert_eq!(
                        response.status(),
                        hyper::StatusCode::OK,
                        "list-modules request did not succeed"
                    );
                    Ok::<_, Error>(response.into_body().concat2().map_err(|err| {
                        format!("could not execute list-modules request: {}", err).into()
                    }))
                })
                .flatten()
                .and_then(|body| {
                    let _: management::models::ModuleList = serde_json::from_slice(&*body)
                        .map_err(|err| format!("could not parse list-modules response: {}", err))?;
                    Ok::<_, Error>(())
                });

            runtime.block_on(f)?;
        }

        ("iothub", Some(matches)) => {
            let iothub_hostname = matches.value_of("hostname").expect("parameter is required");

            let port = matches.value_of("port").expect("parameter is required");

            let port = port
                .parse()
                .map_err(|err| format!("could not parse port: {}", err))?;

            let iothub_host = (iothub_hostname, port)
                .to_socket_addrs()
                .map_err(|err| format!("could not resolve Azure IoT Hub hostname: {}", err))?
                .next()
                .ok_or_else(|| {
                    "could not resolve Azure IoT Hub hostname: no addresses found".to_owned()
                })?;

            let _ = TcpStream::connect_timeout(&iothub_host, std::time::Duration::from_secs(10))
                .map_err(|err| format!("could not connect to IoT Hub: {}", err))?;
        }

        ("local-time", _) => {
            println!(
                "{}",
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap()
                    .as_secs()
            );
        }

        (subcommand, _) => panic!("unexpected subcommand {}", subcommand),
    }

    Ok(())
}

struct Error(String);

impl std::fmt::Debug for Error {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.0)
    }
}

impl From<String> for Error {
    fn from(s: String) -> Self {
        Error(s)
    }
}
