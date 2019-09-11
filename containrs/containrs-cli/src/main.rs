use clap::{App, AppSettings, Arg, SubCommand};

use containrs::reference::RawReference;

fn main() {
    pretty_env_logger::init();

    let app_m = App::new("containrs-cli")
        .setting(AppSettings::ArgRequiredElseHelp)
        .version("0.1.0")
        .author("Daniel Prilik <danielprilik@gmail.com>")
        .about("CLI for interacting with the containrs library.")
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

    let default_repo = "docker.io";
    let default_tag = "latest";
    let docker_compat = true;

    match app_m.subcommand() {
        ("download", Some(sub_m)) => {
            // won't panic, as these are required arguments
            let outdir = sub_m.value_of("outdir").unwrap();
            let image = sub_m.value_of("image").unwrap();

            // TODO: make the directory if it doesn't exist

            let image = match image.parse::<RawReference>() {
                Ok(img) => {
                    println!("{:#?}", img);
                    img.canonicalize(default_repo, default_tag, docker_compat)
                }
                Err(e) => {
                    eprintln!("Error: {}", e);
                    return;
                }
            };

            println!("{:#?}", image);
        }
        _ => unreachable!(),
    }
}
