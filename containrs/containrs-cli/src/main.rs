use clap::{App, AppSettings, Arg, SubCommand};
use hyper::Client as HyperClient;
use hyper_tls::HttpsConnector;

use docker_reference::RawReference;

use containrs::{Client, Credentials};

type Result<T> = std::result::Result<T, Box<dyn std::error::Error + Send + Sync>>;

#[tokio::main]
async fn main() -> Result<()> {
    pretty_env_logger::init();

    let app_m = App::new("containrs-cli")
        .setting(AppSettings::ArgRequiredElseHelp)
        .version("0.1.0")
        .author("Azure IoT Edge Devs")
        .about("CLI for interacting with the containrs library.")
        // TODO: pass authentication credentials via CLI
        .subcommand(
            SubCommand::with_name("test_auth")
                .about("Test if basic authentication works")
                .arg(
                    Arg::with_name("registry")
                        .help("Registry URL (e.g: registry-1.docker.io")
                        .required(true)
                        .index(1),
                ),
        )
        .subcommand(
            SubCommand::with_name("catalog")
                .about("Retrieve a sorted, json list of repositories available in the registry")
                .arg(
                    Arg::with_name("registry")
                        .help("Registry URL (e.g: registry-1.docker.io")
                        .required(true)
                        .index(1),
                ),
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

    let default_repo = "registry-1.docker.io";
    let docker_compat = true;

    let credentials = Credentials::Anonymous;

    match app_m.subcommand() {
        ("test_auth", Some(sub_m)) => {
            // won't panic, as this is a required argument
            let registry = sub_m.value_of("registry").unwrap();
            let mut client = Client::new(hyper_client, registry, credentials);
            println!(
                "Basic auth {}",
                if client.check_basic_auth().await? {
                    "succeeded"
                } else {
                    "failed"
                }
            );
        }
        ("catalog", Some(sub_m)) => {
            // won't panic, as this is a required argument
            let registry = sub_m.value_of("registry").unwrap();

            let mut client = Client::new(hyper_client, registry, credentials);
            match client.get_catalog(None).await? {
                Some((catalog, _)) => println!("{:#?}", catalog),
                None => println!("Registry doesn't support the _catalog endpoint"),
            }
        }
        ("download", Some(sub_m)) => {
            // won't panic, as these are required arguments
            let outdir = sub_m.value_of("outdir").unwrap();
            let image = sub_m.value_of("image").unwrap();

            let image = match image.parse::<RawReference>() {
                Ok(img) => {
                    println!("non-canonical: {:#?}", img);
                    img.canonicalize(default_repo, docker_compat)
                }
                Err(e) => {
                    eprintln!("Error: {}", e);
                    return Ok(());
                }
            };

            println!("canonical: {:#?}", image);

            let mut client = Client::new(hyper_client, image.registry(), credentials);
            let manifest = client.get_manifest(&image).await?;
            println!("{:#?}", manifest);

            // dump manifest to file (making director if it doesn't exist)
            let _ = outdir;
        }
        _ => unreachable!(),
    }

    Ok(())
}
