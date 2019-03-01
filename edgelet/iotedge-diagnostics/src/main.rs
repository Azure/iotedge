// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::use_self)]

#[macro_use]
extern crate clap;

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
                    clap::Arg::with_name("MANAGEMENT_URI")
                        .long("--management-uri")
                        .required(true)
                        .takes_value(true)
                        .help("URI of management endpoint"),
                ),
        )
        .subcommand(clap::SubCommand::with_name("local-time").about("print local time"))
        .subcommand(
            clap::SubCommand::with_name("idle-module")
                .about("idle for some time, then exit")
                .arg(
                    clap::Arg::with_name("DURATION")
                        .long("--duration")
                        .required(true)
                        .takes_value(true)
                        .help("How long to idle for, in seconds"),
                ),
        )
        .subcommand(
            clap::SubCommand::with_name("resolve-module")
                .about("tries to resolve the specified module")
                .arg(
                    clap::Arg::with_name("HOSTNAME")
                        .long("--hostname")
                        .required(true)
                        .takes_value(true)
                        .help("The hostname to resolve"),
                ),
        );

    let matches = app.get_matches();

    let mut runtime = tokio::runtime::Runtime::new()
        .map_err(|err| format!("could not create tokio runtime: {}", err))?;

    match matches.subcommand() {
        ("edge-agent", Some(matches)) => {
            let management_uri = matches
                .value_of("MANAGEMENT_URI")
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

        ("local-time", _) => {
            println!(
                "{}",
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap()
                    .as_secs()
            );
        }

        ("idle-module", Some(matches)) => {
            let duration = matches.value_of("DURATION").expect("parameter is required");
            let duration: u64 = duration
                .parse()
                .map_err(|err| format!("could not parse duration: {}", err))?;

            std::thread::sleep(std::time::Duration::from_secs(duration));
        }

        ("resolve-module", Some(matches)) => {
            let hostname = matches.value_of("HOSTNAME").expect("parameter is required");

            let _ = std::net::ToSocketAddrs::to_socket_addrs(&(hostname, 80))
                .map_err(|err| format!("could not resolve {}: {}", hostname, err))?
                .next()
                .ok_or_else(|| format!("could not resolve {}: no addresses found", hostname))?;
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
