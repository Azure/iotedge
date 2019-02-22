// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

#[macro_use]
extern crate clap;

use futures::{Future, Stream};

use edgelet_core::UrlExt;

#[cfg(unix)]
use hyperlocal::{UnixConnector, Uri as UnixUri};
#[cfg(windows)]
use hyperlocal_windows::{UnixConnector, Uri as UnixUri};

fn main() {
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

    let mut runtime = tokio::runtime::Runtime::new().expect("could not create tokio runtime");

    match matches.subcommand() {
        ("edge-agent", Some(matches)) => {
            let management_uri = matches.value_of("MANAGEMENT_URI").expect("unreachable");
            let management_uri =
                url::Url::parse(management_uri).expect("could not parse management URI");

            let list_modules_response = match management_uri.scheme() {
                "unix" => {
                    let client =
                        hyper::Client::builder().build::<_, hyper::Body>(UnixConnector::new());
                    let uri = UnixUri::new(
                        management_uri
                            .to_uds_file_path()
                            .expect("couldn't get file path from URI"),
                        "/modules/?api-version=2018-06-28",
                    );
                    client.get(uri.into())
                }

                "http" => {
                    let client = hyper::Client::new();
                    let uri = management_uri
                        .join("/modules/?api-version=2018-06-28")
                        .expect("could not construct list-modules request URI");
                    client.get(uri.to_string().parse().expect(
                        "could not convert list-modules request URI from url::Url to hyper::Uri",
                    ))
                }

                scheme => panic!(
                    "unrecognized scheme {:?} in management URI {:?}",
                    scheme, management_uri
                ),
            };

            let f = list_modules_response
                .then(|response| {
                    let response = response.expect("could not execute list-modules request");
                    assert_eq!(
                        response.status(),
                        hyper::StatusCode::OK,
                        "list-modules request did not succeed"
                    );
                    response.into_body().concat2()
                })
                .then(|body| {
                    let body = body.expect("could not execute list-modules request");
                    let _: management::models::ModuleList = serde_json::from_slice(&*body)
                        .expect("could not parse list-modules response");
                    Ok::<_, ()>(())
                });

            runtime.block_on(f).unwrap();
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
            let duration = matches.value_of("DURATION").expect("unreachable");
            let duration: u64 = duration.parse().expect("could not parse duration");

            std::thread::sleep(std::time::Duration::from_secs(duration));
        }

        ("resolve-module", Some(matches)) => {
            let hostname = matches.value_of("HOSTNAME").expect("unreachable");

            let _ = std::net::ToSocketAddrs::to_socket_addrs(&(hostname, 80))
                .unwrap_or_else(|err| panic!("could not resolve {}: {}", hostname, err))
                .next()
                .unwrap_or_else(|| panic!("could not resolve {}: no addresses found", hostname));
        }

        (subcommand, _) => panic!("unexpected subcommand {}", subcommand),
    }
}
