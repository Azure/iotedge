use clap::{App, AppSettings, Arg, SubCommand};
use hyper::Client as HyperClient;
use hyper_tls::HttpsConnector;

use docker_reference::Reference;

use containrs::{Client, Credentials};

#[tokio::main]
async fn main() {
    if let Err(fail) = true_main().await {
        println!("{}", fail);
        for cause in fail.iter_causes() {
            println!("\tcaused by: {}", cause);
        }
    }
}

async fn true_main() -> Result<(), failure::Error> {
    pretty_env_logger::init();

    let app_m = App::new("containrs-cli")
        .setting(AppSettings::ArgRequiredElseHelp)
        .version("0.1.0")
        .author("Azure IoT Edge Devs")
        .about("CLI for interacting with the containrs library.")
        .arg(
            Arg::with_name("default-registry")
                .help("Default registry (defaults to https://registry-1.docker.io/v2/)")
                .long("default-registry")
                .takes_value(true),
        )
        // TODO: pass authentication credentials via CLI
        .subcommand(
            SubCommand::with_name("test_auth")
                .about("Test if authentication works with a particular endpoint")
                .arg(
                    Arg::with_name("endpoint")
                        .help("endpoint URL (e.g: /v2/")
                        .required(true)
                        .index(1),
                ),
        )
        .subcommand(
            SubCommand::with_name("catalog")
                .about("Retrieve a sorted, json list of repositories available in the registry"),
        )
        .subcommand(
            SubCommand::with_name("download")
                .about("Downloads an image from a repository")
                .arg(
                    Arg::with_name("outdir")
                        .help("Output directory")
                        .required(true)
                        .index(1),
                )
                .arg(
                    Arg::with_name("image")
                        .help("Image to download")
                        .required(true)
                        .index(2),
                ),
        )
        .get_matches();

    let https = HttpsConnector::new().expect("TLS initialization failed");
    let hyper_client = HyperClient::builder().build::<_, hyper::Body>(https);

    let default_registry = app_m
        .value_of("default-registry")
        .unwrap_or("https://registry-1.docker.io");
    let docker_compat = true;

    let credentials = Credentials::Anonymous;

    match app_m.subcommand() {
        ("test_auth", Some(sub_m)) => {
            // won't panic, as this is a required argument
            let endpoint = sub_m.value_of("endpoint").unwrap();

            let mut client = Client::new(hyper_client, default_registry, credentials)?;
            println!(
                "Basic auth {}",
                if client.check_authentication(&endpoint, "GET").await? {
                    "succeeded"
                } else {
                    "failed"
                }
            );
        }
        ("catalog", Some(_sub_m)) => {
            let mut client = Client::new(hyper_client, default_registry, credentials)?;
            match client.get_catalog(None).await? {
                Some((catalog, _)) => println!("{:#?}", catalog),
                None => println!("Registry doesn't support the _catalog endpoint"),
            }
        }
        ("download", Some(sub_m)) => {
            // won't panic, as these are required arguments
            let outdir = sub_m.value_of("outdir").unwrap();
            let image = sub_m.value_of("image").unwrap();

            let image = Reference::parse(image, default_registry, docker_compat)?;

            println!("canonical: {:#?}", image);

            let mut client = Client::new(hyper_client, image.registry(), credentials)?;
            let manifest = client.get_manifest(&image).await?;
            println!("{:#?}", manifest);

            // dump manifest to file (making director if it doesn't exist)
            let _ = outdir;
        }
        _ => unreachable!(),
    }

    Ok(())
}
