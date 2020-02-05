use std::collections::HashMap;
use std::process::Stdio;

use clap::{App, AppSettings, Arg, SubCommand};
use futures::{Stream, StreamExt};
use log::*;
use tokio::io::BufReader;
use tokio::prelude::*;
use tokio::process::Command;

use shellrt_api::v0::{
    client::{Input, Output},
    request, VERSION,
};

use docker_reference::Reference;

#[tokio::main]
async fn main() {
    if let Err(fail) = true_main().await {
        println!("Error: {}", fail);
        for cause in fail.iter_causes() {
            println!("\tcaused by: {}", cause);
        }
    }
}

async fn true_main() -> Result<(), failure::Error> {
    pretty_env_logger::init();

    let app_m = App::new("shellrt-driver")
        .setting(AppSettings::SubcommandRequiredElseHelp)
        .version("0.1.0")
        .author("Azure IoT Edge Devs")
        .about("CLI for interacting with the containrs library.")
        .arg(
            Arg::with_name("plugin")
                .help("Path to plugin executable")
                .required(true)
                .index(1),
        )
        .arg(
            Arg::with_name("username")
                .help("Username (for use with UserPass Credentials)")
                .short("u")
                .long("username")
                .takes_value(true),
        )
        .arg(
            Arg::with_name("password")
                .help("Password (for use with UserPass Credentials)")
                .short("p")
                .long("password")
                .takes_value(true),
        )
        .subcommand(
            SubCommand::with_name("img_pull")
                .about("Pull an image using the specified docker-style reference")
                .arg(
                    Arg::with_name("image")
                        .help("Image reference")
                        .required(true)
                        .index(1),
                ),
        )
        .subcommand(
            SubCommand::with_name("img_remove")
                .about("Remove an image using the specified docker-style reference")
                .arg(
                    Arg::with_name("image")
                        .help("Image reference")
                        .required(true)
                        .index(1),
                ),
        )
        // TODO: fill in `shellrt-driver create` CLI arguments!
        .subcommand(
            SubCommand::with_name("create")
                .about("Create a new module for a specific runtime")
                .setting(AppSettings::SubcommandRequiredElseHelp)
                .subcommand(
                    SubCommand::with_name("containerd-cri")
                        .about("Create a new module using containerd-cri")
                        .arg(
                            Arg::with_name("name")
                                .help("Module name")
                                .required(true)
                                .index(1),
                        )
                        .arg(
                            Arg::with_name("image")
                                .help("Image reference")
                                .required(true)
                                .index(2),
                        ),
                ),
        )
        .subcommand(
            SubCommand::with_name("remove")
                .about("Remove a module")
                .arg(
                    Arg::with_name("name")
                        .help("Module name")
                        .required(true)
                        .index(1),
                ),
        )
        .subcommand(
            SubCommand::with_name("start").about("Start a module").arg(
                Arg::with_name("name")
                    .help("Module name")
                    .required(true)
                    .index(1),
            ),
        )
        .subcommand(
            SubCommand::with_name("stop")
                .about("Stop a module")
                .arg(
                    Arg::with_name("name")
                        .help("Module name")
                        .required(true)
                        .index(1),
                )
                .arg(
                    Arg::with_name("timeout")
                        .help("Timeout (in seconds) before the container is forcibly terminated")
                        .index(2),
                ),
        )
        .subcommand(
            SubCommand::with_name("status")
                .about("Query the runtime status of a module")
                .arg(
                    Arg::with_name("name")
                        .help("Module name")
                        .required(true)
                        .index(1),
                ),
        )
        .subcommand(SubCommand::with_name("list").about("List all registered modules"))
        .subcommand(
            SubCommand::with_name("logs")
                .about("View a module's logs")
                .arg(
                    Arg::with_name("name")
                        .help("Module name")
                        .required(true)
                        .index(1),
                )
                .arg(
                    Arg::with_name("follow")
                        .help(
                            "Keep the stream open, returning new log data as it becomes available",
                        )
                        .short("f"),
                )
                .arg(
                    Arg::with_name("since")
                        .help("Only return logs since this time (as a unix timestamp)")
                        .long("since")
                        .takes_value(true),
                )
                .arg(
                    Arg::with_name("tail")
                        .help("Only return the last n lines of the log file")
                        .short("n")
                        .takes_value(true),
                ),
        )
        .subcommand(
            SubCommand::with_name("version").about("Retrieve the runtime's version information"),
        )
        .get_matches();

    let username = app_m.value_of("username");
    let password = app_m.value_of("password");

    let credentials = {
        let mut m = HashMap::new();
        if let Some(username) = username {
            m.insert("username".to_string(), username.to_string());
        }
        if let Some(password) = password {
            m.insert("password".to_string(), password.to_string());
        }
        m
    };

    let plugin = Plugin {
        bin: app_m.value_of("plugin").unwrap().to_string(),
    };

    match app_m.subcommand() {
        ("img_pull", Some(sub_m)) => {
            let image = sub_m
                .value_of("image")
                .expect("image should be a required argument");

            let image = image.parse::<Reference>()?;

            let _res = plugin
                .oneshot(request::ImgPull {
                    image: image.to_string(),
                    credentials,
                })
                .await?;

            println!("the image was pulled successfully");
        }
        ("img_remove", Some(sub_m)) => {
            let image = sub_m
                .value_of("image")
                .expect("image should be a required argument");

            let image = image.parse::<Reference>()?;

            let _res = plugin
                .oneshot(request::ImgRemove {
                    image: image.to_string(),
                })
                .await?;

            println!("the image was removed successfully");
        }
        ("create", Some(sub_m)) => {
            let sub_m = match sub_m.subcommand() {
                ("containerd-cri", Some(x)) => x,
                (rt, _) => unimplemented!("create for runtime {} isn't implemented yet", rt),
            };

            let name = sub_m
                .value_of("name")
                .expect("name should be a required argument");
            let image = sub_m
                .value_of("image")
                .expect("image should be a required argument");

            let image = image.parse::<Reference>()?;

            let _res = plugin
                .oneshot(request::Create {
                    name: name.to_string(),
                    config_type: "containerd-cri".to_string(),
                    env: HashMap::new(), // TODO: support passing custom env vars via cli
                    // TODO: settle on a well-typed spec for containerd-cri Create.config
                    config: serde_json::json!({
                        "image": image.to_string()
                    }),
                })
                .await?;

            println!("the module was created successfully");
        }
        ("remove", Some(sub_m)) => {
            let name = sub_m
                .value_of("name")
                .expect("name should be a required argument");

            let _res = plugin
                .oneshot(request::Remove {
                    name: name.to_string(),
                })
                .await?;

            println!("the module was removed successfully");
        }
        ("start", Some(sub_m)) => {
            let name = sub_m
                .value_of("name")
                .expect("name should be a required argument");

            let _res = plugin
                .oneshot(request::Start {
                    name: name.to_string(),
                })
                .await?;

            println!("the module was started successfully");
        }
        ("stop", Some(sub_m)) => {
            let name = sub_m
                .value_of("name")
                .expect("name should be a required argument");
            let timeout = sub_m.value_of("timeout").unwrap_or("0");

            let _res = plugin
                .oneshot(request::Stop {
                    name: name.to_string(),
                    timeout: timeout.parse::<i64>()?,
                })
                .await?;

            println!("the module was stopped successfully");
        }
        ("status", Some(sub_m)) => {
            let name = sub_m
                .value_of("name")
                .expect("name should be a required argument");

            let res = plugin
                .oneshot(request::Status {
                    name: name.to_string(),
                })
                .await?;

            println!("{:#?}", res);
        }
        ("list", Some(_sub_m)) => {
            let res = plugin.oneshot(request::List {}).await?;

            println!("{:?}", res.modules);
        }
        ("logs", Some(sub_m)) => {
            let name = sub_m
                .value_of("name")
                .expect("name should be a required argument");

            let follow = sub_m.is_present("follow");
            let since = sub_m
                .value_of("since")
                .map(|s| s.parse::<i64>())
                .transpose()
                .map_err(|_| failure::err_msg("could not parse --since as i64"))?;
            let tail = sub_m
                .value_of("tail")
                .map(|s| s.parse::<u32>())
                .transpose()
                .map_err(|_| failure::err_msg("could not parse -n as u32"))?;

            let (_res, mut logs) = plugin
                .stream(request::Logs {
                    name: name.to_string(),
                    follow,
                    since,
                    tail,
                })
                .await?;

            println!("------ Log Stream ------");
            while let Some(chunk) = logs.next().await {
                let chunk = chunk?;
                tokio::io::stdout().write_all(&chunk).await?
            }
        }
        ("version", Some(_sub_m)) => {
            let res = plugin.oneshot(request::Version {}).await?;

            println!("{}", res.info);
        }
        _ => unreachable!(),
    }

    Ok(())
}

struct Plugin {
    bin: String,
}

impl Plugin {
    /// Send a Request to the plugin, blocking until the plugin returns some
    /// Output. Fails if output is malformed, there is a version mismatch, or
    /// the operation failed with an error.
    async fn oneshot<Request>(&self, request: Request) -> Result<Request::Response, failure::Error>
    where
        Request: shellrt_api::v0::ReqMarker,
    {
        let mut child = Command::new(&self.bin)
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .spawn()?;

        let mut child_stdin = child.stdin().take().unwrap();
        let mut child_stdout = child.stdout().take().unwrap();

        let input = Input::new(request);
        let req = serde_json::to_vec(&input)?;

        info!("input payload: {:#?}", input);
        debug!("---> {}", String::from_utf8_lossy(&req));

        child_stdin.write(&req).await?;
        std::mem::drop(child_stdin);

        let _status = child.await?;

        let mut res = Vec::new();
        child_stdout.read_to_end(&mut res).await?;
        debug!("<--- {}", String::from_utf8_lossy(&res));

        let output: Output<Request::Response> = serde_json::from_slice(&res)?;

        info!("output payload: {:#?}", output);

        // TODO: use semver for more lenient version compatibility
        if output.version() != VERSION {
            failure::bail!("Bad response: invalid API version");
        }

        output
            .into_inner()
            .map_err(|e| failure::err_msg(format!("API error: {:#?}", e)))
    }

    async fn stream<Request>(
        &self,
        request: Request,
    ) -> Result<
        (
            Request::Response,
            impl Stream<Item = std::io::Result<Vec<u8>>>,
        ),
        failure::Error,
    >
    where
        Request: shellrt_api::v0::ReqMarker,
    {
        let mut child = Command::new(&self.bin)
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .spawn()?;

        let mut child_stdin = child.stdin().take().unwrap();
        let mut child_stdout = BufReader::new(child.stdout().take().unwrap());

        let input = Input::new(request);
        info!("input payload: {:#?}", input);
        let req = serde_json::to_vec(&input)?;
        debug!("---> {}", String::from_utf8_lossy(&req));

        child_stdin.write(&req).await?;
        std::mem::drop(child_stdin);

        tokio::spawn(async {
            let _status = child.await;
        });

        let mut res = Vec::new();
        child_stdout.read_until(b'\x00', &mut res).await?;
        if res.last().cloned() == Some(b'\0') {
            // pop the delimiter
            res.pop();
        };

        debug!("<--- {}", String::from_utf8_lossy(&res));
        let output: Output<Request::Response> = serde_json::from_slice(&res)?;
        info!("output payload: {:#?}", output);

        // TODO: use semver for more lenient version compatibility
        if output.version() != VERSION {
            failure::bail!("Bad response: invalid API version");
        }

        Ok((
            output
                .into_inner()
                .map_err(|e| failure::err_msg(format!("API error: {:#?}", e)))?,
            tokio_util::codec::FramedRead::new(child_stdout, tokio_util::codec::BytesCodec::new())
                .map(|bytes| bytes.map(|b| b.to_vec())),
        ))
    }
}
