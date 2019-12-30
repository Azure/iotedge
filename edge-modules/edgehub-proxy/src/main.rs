// Copyright (c) Microsoft. All rights reserved.
use std::fs;
use std::process;

use clap::{crate_description, crate_name, crate_version, App, AppSettings, Arg, SubCommand};
use edgelet_http::UrlConnector;
use hyper::Client;
use log::info;
use url::Url;

use edgehub_proxy::error::Error;
use edgehub_proxy::logging;
use workload::apis::client::APIClient;
use workload::apis::configuration::Configuration;
use workload::models::ServerCertificateRequest;

fn main() {
    logging::init();
    if let Err(e) = run() {
        logging::log_error(&e);
        process::exit(1);
    }
}

fn run() -> Result<(), Error> {
    let matches = App::new(crate_name!())
        .version(crate_version!())
        .about(crate_description!())
        .setting(AppSettings::SubcommandRequired)
        .setting(AppSettings::SubcommandRequiredElseHelp)
        .setting(AppSettings::ArgRequiredElseHelp)
        .setting(AppSettings::TrailingVarArg)
        .arg(
            Arg::with_name("host")
                .help("Workload socket to connect to")
                .short("H")
                .long("host")
                .takes_value(true)
                .value_name("HOST")
                .required(true)
                .env("IOTEDGE_WORKLOADURI"),
        )
        .arg(
            Arg::with_name("moduleid")
                .help("Module id")
                .short("m")
                .long("moduleid")
                .takes_value(true)
                .value_name("MODULEID")
                .required(true)
                .env("IOTEDGE_MODULEID"),
        )
        .arg(
            Arg::with_name("genid")
                .help("Generation id")
                .short("g")
                .long("genid")
                .takes_value(true)
                .value_name("GENID")
                .required(true)
                .env("IOTEDGE_MODULEGENERATIONID"),
        )
        .arg(
            Arg::with_name("apiversion")
                .help("iotedged API Version")
                .short("a")
                .long("apiversion")
                .takes_value(true)
                .value_name("API_VERSION")
                .required(true)
                .env("IOTEDGE_APIVERSION")
                .default_value("2018-06-28"),
        )
        .subcommand(
            SubCommand::with_name("cert-server")
                .about("Retrieve a server cert")
                .arg(
                    Arg::with_name("common name")
                        .help("Sets the common name of the certificate")
                        .required(true)
                        .long("common-name")
                        .takes_value(true)
                        .value_name("COMMON_NAME")
                        .required(true),
                )
                .arg(
                    Arg::with_name("expiration")
                        .help("Sets the expiration time of the certificate")
                        .required(true)
                        .long("expiration")
                        .takes_value(true)
                        .value_name("EXPIRATION")
                        .required(true),
                )
                .arg(
                    Arg::with_name("crt file")
                        .help("Sets the crt output file")
                        .required(false)
                        .long("crt")
                        .takes_value(true)
                        .value_name("CRT_FILE"),
                )
                .arg(
                    Arg::with_name("key file")
                        .help("Sets the key output file")
                        .required(false)
                        .long("key")
                        .takes_value(true)
                        .value_name("KEY_FILE"),
                )
                .arg(
                    Arg::with_name("combined file")
                        .help("Sets the combined output file")
                        .required(false)
                        .long("combined")
                        .takes_value(true)
                        .value_name("COMBINED_FILE"),
                ),
        )
        .arg(
            Arg::with_name("cmd")
                .help("Command to run after retrieving certificate")
                .multiple(true)
                .global(true)
                .value_name("CMD"),
        )
        .get_matches();

    let mut tokio_runtime = tokio::runtime::current_thread::Runtime::new()?;
    let url = Url::parse(
        matches
            .value_of("host")
            .expect("no value for required HOST"),
    )?;
    let client = client(&url)?;

    let api_version = matches
        .value_of("apiversion")
        .expect("no default value for API_VERSION");
    let module = matches
        .value_of("moduleid")
        .expect("no value for required MODULEID");
    let gen = matches
        .value_of("genid")
        .expect("no value for required GENID");

    if let ("cert-server", Some(args)) = matches.subcommand() {
        let common_name = args
            .value_of("common name")
            .expect("no value for required COMMON_NAME");
        let expiration = args
            .value_of("expiration")
            .expect("no value for required EXPIRATION");
        info!("Retrieving server certificate with common name \"{}\" and expiration \"{}\" from {}...", common_name, expiration, url);

        let cert_request =
            ServerCertificateRequest::new(common_name.to_string(), expiration.to_string());
        let request =
            client
                .workload_api()
                .create_server_certificate(api_version, module, gen, cert_request);

        let response = tokio_runtime.block_on(request)?;
        info!("Retrieved server certificate.");

        if let Some(crt_path) = args.value_of("crt file") {
            fs::write(crt_path, response.certificate())?;
        }

        if let Some(key_path) = args.value_of("key file") {
            if let Some(bytes) = response.private_key().bytes() {
                fs::write(key_path, bytes)?;
            }
        }

        if let Some(combined_path) = args.value_of("combined file") {
            if let Some(bytes) = response.private_key().bytes() {
                fs::write(
                    combined_path,
                    format!("{}{}", response.certificate(), bytes),
                )?;
            }
        }
    }

    if let Some(mut cmd) = matches.values_of("cmd") {
        info!(
            "Executing: {}",
            matches
                .values_of("cmd")
                .expect("cmd")
                .collect::<Vec<&str>>()
                .join(" ")
        );
        if let Some(process) = cmd.next() {
            let mut child = process::Command::new(process).args(cmd).spawn()?;
            child.wait()?;
        }
    }
    Ok(())
}

fn client(url: &Url) -> Result<APIClient, Error> {
    let hyper_client = Client::builder().build(UrlConnector::new(&url)?);
    let base_path = get_base_path(url);
    let mut configuration = Configuration::new(hyper_client);
    configuration.base_path = base_path.to_string();

    let scheme = url.scheme().to_string();
    configuration.uri_composer = Box::new(move |base_path, path| {
        Ok(UrlConnector::build_hyper_uri(&scheme, base_path, path)?)
    });
    let client = APIClient::new(configuration);
    Ok(client)
}

fn get_base_path(url: &Url) -> &str {
    match url.scheme() {
        "unix" => url.path(),
        _ => url.as_str(),
    }
}
