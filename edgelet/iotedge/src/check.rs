// Copyright (c) Microsoft. All rights reserved.

use std;
use std::borrow::Cow;
use std::collections::HashSet;
use std::ffi::{CStr, OsStr, OsString};
use std::fs::File;
use std::net::TcpStream;
use std::path::PathBuf;
use std::process::Command;

use failure::Fail;
use failure::{self, Context, ResultExt};
use futures::future::{self, FutureResult};
use futures::{Future, IntoFuture, Stream};
#[cfg(unix)]
use libc;
use regex::Regex;
use serde_json;

use edgelet_config::{Provisioning, Settings};
use edgelet_core::{self, UrlExt};
use edgelet_docker::DockerConfig;
use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;

use error::{Error, ErrorKind, FetchLatestVersionsReason};
use LatestVersions;

pub struct Check {
    config_file: PathBuf,
    iotedged: PathBuf,
    ntp_server: String,
    steps_override: Option<Vec<String>>,
    latest_versions: super::LatestVersions,

    // These optional fields are populated by the pre-checks
    settings: Option<Settings<DockerConfig>>,
    docker_host_arg: Option<String>,
    iothub_hostname: Option<String>,
}

impl Check {
    pub fn new(
        config_file: PathBuf,
        iotedged: PathBuf,
        ntp_server: String,
        steps_override: Option<Vec<String>>,
    ) -> impl Future<Item = Self, Error = Error> + Send {
        let proxy = std::env::var("HTTPS_PROXY")
            .ok()
            .or_else(|| std::env::var("https_proxy").ok());
        let proxy = if let Some(proxy) = proxy {
            let proxy = proxy
                .parse::<hyper::Uri>()
                .context(ErrorKind::FetchLatestVersions(
                    FetchLatestVersionsReason::CreateClient,
                ));
            match proxy {
                Ok(proxy) => future::ok(Some(proxy)),
                Err(err) => future::err(Error::from(err)),
            }
        } else {
            future::ok(None)
        };

        let hyper_client = proxy.and_then(|proxy| {
            Ok(
                MaybeProxyClient::new(proxy).context(ErrorKind::FetchLatestVersions(
                    FetchLatestVersionsReason::CreateClient,
                ))?,
            )
        });

        let request = hyper::Request::get("https://aka.ms/latest-iotedge-stable")
            .body(hyper::Body::default())
            .expect("can't fail to create request");

        hyper_client
            .and_then(|hyper_client| {
                hyper_client.call(request).then(|response| match response {
                    Ok(response) => Ok((response, hyper_client)),
                    Err(err) => Err(err
                        .context(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::GetResponse,
                        ))
                        .into()),
                })
            })
            .and_then(move |(response, hyper_client)| match response.status() {
                hyper::StatusCode::MOVED_PERMANENTLY => {
                    let uri = response
                        .headers()
                        .get(hyper::header::LOCATION)
                        .ok_or_else(|| {
                            ErrorKind::FetchLatestVersions(
                                FetchLatestVersionsReason::InvalidOrMissingLocationHeader,
                            )
                        })?
                        .to_str()
                        .context(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::InvalidOrMissingLocationHeader,
                        ))?;
                    let request = hyper::Request::get(uri)
                        .body(hyper::Body::default())
                        .expect("can't fail to create request");
                    Ok(hyper_client.call(request).map_err(|err| {
                        err.context(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::GetResponse,
                        ))
                        .into()
                    }))
                }
                status_code => Err(ErrorKind::FetchLatestVersions(
                    FetchLatestVersionsReason::ResponseStatusCode(status_code),
                )
                .into()),
            })
            .flatten()
            .and_then(|response| -> Result<_, Error> {
                match response.status() {
                    hyper::StatusCode::OK => Ok(response.into_body().concat2().map_err(|err| {
                        err.context(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::GetResponse,
                        ))
                        .into()
                    })),
                    status_code => Err(ErrorKind::FetchLatestVersions(
                        FetchLatestVersionsReason::ResponseStatusCode(status_code),
                    )
                    .into()),
                }
            })
            .flatten()
            .and_then(|body| -> Result<_, Error> {
                let latest_versions: LatestVersions = serde_json::from_slice(&body).context(
                    ErrorKind::FetchLatestVersions(FetchLatestVersionsReason::GetResponse),
                )?;
                Ok(Check {
                    config_file,
                    iotedged,
                    ntp_server,
                    steps_override,
                    latest_versions,

                    settings: None,
                    docker_host_arg: None,
                    iothub_hostname: None,
                })
            })
    }

    fn execute_inner(&mut self) -> Result<(), Error> {
        const PRE_CHECK_SECTION_NAME: &str = "pre-check";

        // DEVNOTE: Keep the names of top-level steps in sync with help-text of the "check" subcommand in main.rs (except for "pre-check")
        const CHECKS: &[(
            &str, // Section name
            &[(
                &str,                                                     // Check description
                fn(&mut Check) -> Result<Option<String>, failure::Error>, // Check function. Returns Ok(Some(warning)), Ok(None), or Err(err).
            )],
        )] = &[
            (
                PRE_CHECK_SECTION_NAME,
                &[
                    ("config.yaml is well-formed", parse_settings),
                    (
                        "config.yaml has well-formed connection string",
                        settings_connection_string,
                    ),
                    (
                        "container runtime is installed and functional",
                        container_runtime,
                    ),
                ],
            ),
            (
                "config",
                &[
                    ("config.yaml has correct hostname", settings_hostname),
                    (
                        "config.yaml has well-formed moby runtime URI",
                        settings_moby_runtime_uri,
                    ),
                    (
                        "config.yaml has correct URIs for daemon mgmt endpoint",
                        daemon_mgmt_endpoint_uri,
                    ),
                    (
                        "container runtime network allows name resolution",
                        container_runtime_network,
                    ),
                ],
            ),
            (
                "deps",
                &[
                    ("latest security daemon", iotedged_version),
                    ("latest edge agent container image", |check| {
                        edge_container_version(
                            "edgeAgent",
                            &check.latest_versions.edge_agent,
                            check.docker_host_arg.as_ref().map(AsRef::as_ref),
                        )
                    }),
                    ("latest edge hub container image", |check| {
                        edge_container_version(
                            "edgeHub",
                            &check.latest_versions.edge_hub,
                            check.docker_host_arg.as_ref().map(AsRef::as_ref),
                        )
                    }),
                    ("host time is close to real time", host_local_time),
                    ("container time is close to host time", container_local_time),
                ],
            ),
            (
                "conn",
                &[
                    ("can connect to IoT Hub AMQP port", |check| {
                        connection_to_iot_hub(check, 5671)
                    }),
                    ("can connect to IoT Hub HTTPS port", |check| {
                        connection_to_iot_hub(check, 443)
                    }),
                    ("can connect to IoT Hub MQTT port", |check| {
                        connection_to_iot_hub(check, 8883)
                    }),
                ],
            ),
        ];

        let mut steps_override: HashSet<Cow<'static, str>> =
            if let Some(steps_override) = self.steps_override.take() {
                steps_override.into_iter().map(Into::into).collect()
            } else {
                CHECKS
                    .iter()
                    .map(|&(section_name, _)| section_name.into())
                    .collect()
            };
        steps_override.insert(PRE_CHECK_SECTION_NAME.into());

        let mut have_warnings = false;
        let mut have_errors = false;

        for (section_name, section_checks) in CHECKS {
            if !steps_override.contains(*section_name) {
                continue;
            }

            println!("{}", section_name);
            println!("{}", "-".repeat(section_name.len()));

            for (check_name, check) in *section_checks {
                match check(self) {
                    Ok(None) => println!("\u{221a} {}", check_name),

                    Ok(Some(warning)) => {
                        have_warnings = true;

                        println!("\u{203c} {}", check_name);
                        println!("    {}", warning);
                    }

                    Err(err) => {
                        have_errors = true;

                        println!("\u{00d7} {}", check_name);

                        {
                            let err = err.to_string();
                            let mut lines = err.split('\n');
                            println!("    {}", lines.next().unwrap());
                            for line in lines {
                                println!("    {}", line);
                            }
                        }

                        for cause in err.iter_causes() {
                            let cause = cause.to_string();
                            let mut lines = cause.split('\n');
                            println!("        caused by: {}", lines.next().unwrap());
                            for line in lines {
                                println!("                   {}", line);
                            }
                        }
                    }
                }
            }

            println!();
        }

        match (have_warnings, have_errors) {
            (false, false) => println!("All checks succeeded"),
            (true, false) => println!("One or more checks raised warnings"),
            (_, true) => return Err(ErrorKind::Diagnostics.into()),
        }

        Ok(())
    }
}

impl ::Command for Check {
    type Future = FutureResult<(), Error>;

    fn execute(&mut self) -> Self::Future {
        self.execute_inner().into_future()
    }
}

fn parse_settings(check: &mut Check) -> Result<Option<String>, failure::Error> {
    let config_file = &check.config_file;

    // The config crate just returns a "file not found" error when it can't open the file for any reason,
    // even if the real error was a permissions issue.
    //
    // So we first try to open the file for reading ourselves.
    let _ = File::open(config_file)
        .with_context(|_| format!("could not open {}", config_file.display()))?;

    let settings = Settings::new(Some(config_file))
        .with_context(|_| format!("could not parse {}", config_file.display()))?;

    check.settings = Some(settings);

    Ok(None)
}

fn settings_connection_string(check: &mut Check) -> Result<Option<String>, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    if let Provisioning::Manual(manual) = settings.provisioning() {
        let (_, _, hub) = manual.parse_device_connection_string()?;
        check.iothub_hostname = Some(hub.to_string());
    }

    Ok(None)
}

fn container_runtime(check: &mut Check) -> Result<Option<String>, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let uri = settings.moby_runtime().uri();

    let docker_host_arg = match uri.scheme() {
        "unix" => uri.to_string(),

        "npipe" => {
            let mut uri = uri.to_string();
            uri.replace_range(0.."npipe://".len(), "npipe:////");
            uri
        }

        scheme => {
            return Err(Context::new(format!(
                "unrecognized URI scheme for moby_runtime.uri: {}",
                scheme
            ))
            .into());
        }
    };

    let output = docker(&docker_host_arg, &["version"])?;
    if !output.status.success() {
        return Err(Context::new(format!(
            "docker returned {}, stderr = {}",
            output.status,
            String::from_utf8_lossy(&*output.stderr)
        ))
        .into());
    }

    check.docker_host_arg = Some(docker_host_arg);

    Ok(None)
}

fn settings_hostname(check: &mut Check) -> Result<Option<String>, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let config_hostname = settings.hostname();

    let machine_hostname = unsafe {
        let mut result = vec![0_u8; 256];

        #[cfg(unix)]
        {
            if libc::gethostname(result.as_mut_ptr() as _, result.len()) != 0 {
                return Err(std::io::Error::last_os_error() // Calls errno
                    .context("could not get hostname")
                    .into());
            }
        }

        #[cfg(windows)]
        #[allow(clippy::cast_possible_truncation, clippy::cast_possible_wrap)]
        {
            // libstd only calls WSAStartup when something under std::net gets used, like creating a TcpStream.
            // Since we haven't done anything like that up to this point, it ends up not being called.
            // So we call it manually.
            //
            // The process is going to exit anyway, so there's no reason to make the effort of
            // calling the corresponding WSACleanup later.
            let mut wsa_data: winapi::um::winsock2::WSADATA = std::mem::zeroed();
            match winapi::um::winsock2::WSAStartup(0x202, &mut wsa_data) {
                0 => (),
                result => {
                    return Err(Context::new(format!("WSAStartup failed with {}", result)).into());
                }
            }

            if winapi::um::winsock2::gethostname(result.as_mut_ptr() as _, result.len() as _) != 0 {
                // Can't use std::io::Error::last_os_error() because that calls GetLastError, not WSAGetLastError
                let winsock_err = winapi::um::winsock2::WSAGetLastError();

                return Err(Context::new(format!(
                    "gethostname failed with last error {}",
                    winsock_err
                ))
                .into());
            }
        }

        let nul_index = result
            .iter()
            .position(|&b| b == b'\0')
            .ok_or_else(|| Context::new("gethostname did not return NUL-terminated string"))?;

        let result = CStr::from_bytes_with_nul_unchecked(&result[..=nul_index]);

        let result = result.to_str().context("could not get hostname")?;

        result.to_string()
    };

    if config_hostname != machine_hostname {
        return Err(Context::new(format!(
            "machine has hostname {} but config has hostname {}",
            machine_hostname, config_hostname
        ))
        .into());
    }

    Ok(None)
}

fn settings_moby_runtime_uri(check: &mut Check) -> Result<Option<String>, failure::Error> {
    if !cfg!(windows) {
        return Ok(None);
    }

    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let moby_runtime_uri = settings.moby_runtime().uri().to_string();

    if moby_runtime_uri != "npipe://./pipe/iotedge_moby_engine" {
        return Ok(Some(format!(
            "moby_runtime.uri {:?} is not supported for production",
            moby_runtime_uri
        )));
    }

    Ok(None)
}

fn daemon_mgmt_endpoint_uri(check: &mut Check) -> Result<Option<String>, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
        docker_host_arg
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let connect_management_uri = settings.connect().management_uri();
    let listen_management_uri = settings.listen().management_uri();

    let mut args: Vec<Cow<'_, OsStr>> = vec![
        Cow::Borrowed(OsStr::new("run")),
        Cow::Borrowed(OsStr::new("--rm")),
    ];

    for (name, value) in settings.agent().env() {
        args.push(Cow::Borrowed(OsStr::new("-e")));
        args.push(Cow::Owned(format!("{}={}", name, value).into()));
    }

    match (connect_management_uri.scheme(), listen_management_uri.scheme()) {
        ("http", "http") => (),

        ("unix", "unix") => {
            args.push(Cow::Borrowed(OsStr::new("-v")));

            // On Windows we mount the parent folder because we can't mount the socket files directly

            let container_path = connect_management_uri.to_uds_file_path().context("could not parse connect.management_uri as file path")?;
            #[cfg(windows)]
            let container_path = container_path.parent().ok_or_else(|| Context::new("connect.management_uri is not a valid file path - does not have a parent directory"))?;
            let container_path = container_path.to_str().ok_or_else(|| Context::new("connect.management_uri is a unix socket, but the file path is not valid utf-8"))?;

            let host_path = listen_management_uri.to_uds_file_path().context("could not parse listen.management_uri as file path")?;
            #[cfg(windows)]
            let host_path = host_path.parent().ok_or_else(|| Context::new("connect.management_uri is not a valid file path - does not have a parent directory"))?;
            let host_path = host_path.to_str().ok_or_else(|| Context::new("listen.management_uri is a unix socket, but the file path is not valid utf-8"))?;

            args.push(Cow::Owned(format!("{}:{}", host_path, container_path).into()));
        },

        (scheme1, scheme2) if scheme1 != scheme2 => return Err(Context::new(
            format!("config.yaml has different schemes for connect.management_uri ({:?}) and listen.management_uri ({:?})", scheme1, scheme2))
            .into()),

        (scheme, _) => return Err(Context::new(format!("unrecognized scheme {} for connect.management_uri", scheme)).into()),
    }

    args.extend(vec![
        Cow::Owned(OsString::from(format!(
            "azureiotedge-diagnostics:{}",
            edgelet_core::version()
        ))),
        Cow::Borrowed(OsStr::new("/iotedge-diagnostics")),
        Cow::Borrowed(OsStr::new("edge-agent")),
        Cow::Borrowed(OsStr::new("--management-uri")),
        Cow::Owned(OsString::from(connect_management_uri.to_string())),
    ]);

    let output = docker(docker_host_arg, args)?;
    if !output.status.success() {
        return Err(Context::new(format!(
            "docker returned {}, stderr = {}",
            output.status,
            String::from_utf8_lossy(&*output.stderr)
        ))
        .into());
    }

    Ok(None)
}

fn container_runtime_network(check: &mut Check) -> Result<Option<String>, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
        docker_host_arg
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let network_name = settings.moby_runtime().network();

    let mut module1_process = Command::new("docker");
    module1_process.arg("-H");
    module1_process.arg(docker_host_arg);

    module1_process.args(vec![
        "run",
        "--rm",
        "--network",
        network_name,
        "--name",
        "diagnostics-1",
    ]);
    module1_process.arg(format!(
        "azureiotedge-diagnostics:{}",
        edgelet_core::version()
    ));
    module1_process.args(vec![
        "/iotedge-diagnostics",
        "idle-module",
        "--duration",
        "10",
    ]);

    // Let it run in the background
    module1_process
        .spawn()
        .with_context(|_| format!("could not run {:?}", module1_process))?;

    let module2_output = docker(
        docker_host_arg,
        vec![
            Cow::Borrowed(OsStr::new("run")),
            Cow::Borrowed(OsStr::new("--rm")),
            Cow::Borrowed(OsStr::new("--network")),
            Cow::Borrowed(OsStr::new(network_name)),
            Cow::Borrowed(OsStr::new("--name")),
            Cow::Borrowed(OsStr::new("diagnostics-2")),
            Cow::Owned(OsString::from(format!(
                "azureiotedge-diagnostics:{}",
                edgelet_core::version()
            ))),
            Cow::Borrowed(OsStr::new("/iotedge-diagnostics")),
            Cow::Borrowed(OsStr::new("resolve-module")),
            Cow::Borrowed(OsStr::new("--hostname")),
            Cow::Borrowed(OsStr::new("diagnostics-1")),
        ],
    )?;
    if !module2_output.status.success() {
        return Err(Context::new(format!(
            "docker returned {}, stderr = {}",
            module2_output.status,
            String::from_utf8_lossy(&*module2_output.stderr)
        ))
        .into());
    }

    Ok(None)
}

fn iotedged_version(check: &mut Check) -> Result<Option<String>, failure::Error> {
    let mut process = Command::new(&check.iotedged);
    process.arg("--version");

    if cfg!(windows) {
        process.env("IOTEDGE_RUN_AS_CONSOLE", "true");
    }

    let output = process
        .output()
        .with_context(|_| format!("could not run {:?}", process))?;
    if !output.status.success() {
        return Err(Context::new(format!(
            "iotedged returned {}, stderr = {}",
            output.status,
            String::from_utf8_lossy(&*output.stderr)
        ))
        .into());
    }

    let output =
        String::from_utf8(output.stdout).context("could not parse output of iotedged --version")?;

    let iotedged_version_regex = Regex::new(r"^iotedged ([^ ]+)(?: \(.*\))?$")
        .expect("This hard-coded regex is expected to be valid.");
    let captures = iotedged_version_regex.captures(output.trim()).ok_or_else(||
        Context::new("could not parse output of iotedged --version {:?} : does not match expected format"))?;
    let version = captures
        .get(1)
        .expect("unreachable: regex defines one capturing group")
        .as_str();

    if version != check.latest_versions.iotedged {
        return Err(Context::new(format!(
            "expected iotedged to have version {} but it has version {}",
            check.latest_versions.iotedged, version
        ))
        .into());
    }

    Ok(None)
}

fn edge_container_version(
    container_name: &str,
    expected_version: &str,
    docker_host_arg: Option<&str>,
) -> Result<Option<String>, failure::Error> {
    #[derive(Deserialize)]
    struct VersionInfo {
        version: String,
    }

    let docker_host_arg = if let Some(docker_host_arg) = docker_host_arg {
        docker_host_arg
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let output = if cfg!(windows) {
        docker(
            docker_host_arg,
            vec![
                "exec",
                container_name,
                "cmd",
                "/C",
                "type",
                r"C:\app\versionInfo.json",
            ],
        )?
    } else {
        docker(
            docker_host_arg,
            vec!["exec", container_name, "cat", "/app/versionInfo.json"],
        )?
    };
    if !output.status.success() {
        return Err(Context::new(format!(
            "docker returned {}, stderr = {}",
            output.status,
            String::from_utf8_lossy(&*output.stderr)
        ))
        .into());
    }

    let version_info: VersionInfo = serde_json::from_slice(&output.stdout)?;
    if version_info.version != expected_version {
        return Err(Context::new(format!(
            "expected {} to have version {} but it has version {}",
            container_name, expected_version, version_info.version
        ))
        .into());
    }

    Ok(None)
}

fn host_local_time(check: &mut Check) -> Result<Option<String>, failure::Error> {
    let mini_sntp::SntpTimeQueryResult {
        local_clock_offset, ..
    } = mini_sntp::query(&check.ntp_server)?;

    if local_clock_offset.num_seconds() >= 10 {
        return Err(Context::new(format!(
            "detected large difference between host local time and real time: {}",
            local_clock_offset
        ))
        .into());
    }

    Ok(None)
}

fn container_local_time(check: &mut Check) -> Result<Option<String>, failure::Error> {
    let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
        docker_host_arg
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let expected_duration = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .context("could not query local time of host")?;

    let output = docker(
        docker_host_arg,
        vec![
            Cow::Borrowed(OsStr::new("run")),
            Cow::Borrowed(OsStr::new("--rm")),
            Cow::Owned(OsString::from(format!(
                "azureiotedge-diagnostics:{}",
                edgelet_core::version()
            ))),
            Cow::Borrowed(OsStr::new("/iotedge-diagnostics")),
            Cow::Borrowed(OsStr::new("local-time")),
        ],
    )?;
    if !output.status.success() {
        return Err(Context::new(format!(
            "docker returned {}, stderr = {}",
            output.status,
            String::from_utf8_lossy(&*output.stderr)
        ))
        .into());
    }

    let output = &output.stdout;
    let output = std::str::from_utf8(output)
        .with_context(|_| format!("could not parse container output {:?}", output))?;
    let output = output
        .trim_end()
        .parse::<u64>()
        .with_context(|_| format!("could not parse container output {:?}", output))?;
    let actual_duration = std::time::Duration::from_secs(output);

    let diff = std::cmp::max(actual_duration, expected_duration)
        - std::cmp::min(actual_duration, expected_duration);
    if diff.as_secs() >= 10 {
        return Err(Context::new(format!(
            "detected large difference between host local time {:?} and container local time {:?}",
            expected_duration, actual_duration,
        ))
        .into());
    }

    Ok(None)
}

fn connection_to_iot_hub(check: &mut Check, port: u16) -> Result<Option<String>, failure::Error> {
    let iothub_hostname = if let Some(iothub_hostname) = &check.iothub_hostname {
        iothub_hostname
    } else {
        return Ok(Some("skipping because of previous failures".to_string()));
    };

    let iothub_host = std::net::ToSocketAddrs::to_socket_addrs(&(&**iothub_hostname, port))
        .with_context(|_| "could not resolve Azure IoT Hub hostname")?
        .next()
        .ok_or_else(|| {
            Context::new("could not resolve Azure IoT Hub hostname: no addresses found")
        })?;

    let _ = TcpStream::connect_timeout(&iothub_host, std::time::Duration::from_secs(10))
        .context("could not connect to IoT Hub")?;

    Ok(None)
}

fn docker<I>(docker_host_arg: &str, args: I) -> Result<std::process::Output, failure::Error>
where
    I: IntoIterator,
    <I as IntoIterator>::Item: AsRef<OsStr>,
{
    let mut process = Command::new("docker");
    process.arg("-H");
    process.arg(docker_host_arg);

    process.args(args);

    Ok(process
        .output()
        .with_context(|_| format!("could not run {:?}", process))?)
}

#[cfg(test)]
mod tests {
    #[test]
    fn config_file_checks_ok() {
        let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();

        for filename in &[
            "sample_settings.yaml",
            "sample_settings.dps.sym.yaml",
            "sample_settings.tg.yaml",
        ] {
            let config_file = format!(
                "{}/../edgelet-config/test/{}/{}",
                env!("CARGO_MANIFEST_DIR"),
                if cfg!(windows) { "windows" } else { "linux" },
                filename
            );

            let mut check = runtime
                .block_on(super::Check::new(
                    config_file.into(),
                    "iotedged".into(),             // unused for this test
                    "pool.ntp.org:123".to_owned(), // unused for this test
                    None,
                ))
                .unwrap();

            match super::parse_settings(&mut check) {
                Ok(None) => (),
                Ok(Some(warning)) => panic!(
                    "parsing {} failed with an unexpected warning: {}",
                    filename, warning
                ),
                Err(err) => panic!(
                    "parsing {} failed with an unexpected error: {}",
                    filename, err
                ),
            }

            match super::settings_connection_string(&mut check) {
                Ok(None) => (),
                Ok(Some(warning)) => panic!(
                    "checking connection string in {} failed with an unexpected warning: {}",
                    filename, warning
                ),
                Err(err) => panic!(
                    "checking connection string in {} failed with an unexpected error: {}",
                    filename, err
                ),
            }

            match super::settings_hostname(&mut check) {
                Ok(None) => panic!("checking hostname in {} succeeded unexpectedly", filename),
                Ok(Some(warning)) => panic!(
                    "checking connection string in {} succeeded unexpectedly with a warning: {}",
                    filename, warning
                ),
                Err(err) => assert!(
                    err.to_string()
                        .contains("but config has hostname localhost"),
                    "checking hostname in {} produced unexpected error: {}",
                    filename,
                    err,
                ),
            }

            match super::settings_moby_runtime_uri(&mut check) {
                Ok(None) => (),

                Ok(Some(warning)) => panic!(
                    "checking moby_runtime.uri in {} failed with an unexpected warning: {}",
                    filename, warning
                ),

                Err(err) => panic!(
                    "checking moby_runtime.uri in {} failed with an unexpected error: {}",
                    filename, err
                ),
            }
        }
    }

    #[test]
    fn parse_settings_err() {
        let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();

        let filename = "bad_sample_settings.yaml";
        let config_file = format!(
            "{}/../edgelet-config/test/{}/{}",
            env!("CARGO_MANIFEST_DIR"),
            if cfg!(windows) { "windows" } else { "linux" },
            filename
        );

        let mut check = runtime
            .block_on(super::Check::new(
                config_file.into(),
                "iotedged".into(),             // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                None,
            ))
            .unwrap();

        match super::parse_settings(&mut check) {
            Ok(None) => panic!("parsing {} succeeded unexpectedly", filename),
            Ok(Some(warning)) => panic!(
                "parsing {} succeeded unexpectedly with a warning: {}",
                filename, warning
            ),
            Err(err) => {
                let err = err
                    .iter_causes()
                    .nth(1)
                    .expect("expected to find cause-of-cause-of-error");
                assert!(
                    err.to_string()
                        .contains("while parsing a flow mapping, did not find expected ',' or '}' at line 10 column 5"),
                    "parsing {} produced unexpected error: {}",
                    filename,
                    err,
                );
            }
        }
    }

    #[test]
    #[cfg(windows)]
    fn moby_runtime_uri_windows_wants_moby() {
        let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();

        let filename = "sample_settings_notmoby.yaml";
        let config_file = format!(
            "{}/../edgelet-config/test/{}/{}",
            env!("CARGO_MANIFEST_DIR"),
            if cfg!(windows) { "windows" } else { "linux" },
            filename
        );

        let mut check = runtime
            .block_on(super::Check::new(
                config_file.into(),
                "iotedged".into(),             // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                None,
            ))
            .unwrap();

        match super::parse_settings(&mut check) {
            Ok(None) => (),
            Ok(Some(warning)) => panic!(
                "parsing {} failed with an unexpected warning: {}",
                filename, warning
            ),
            Err(err) => panic!(
                "parsing {} failed with an unexpected error: {}",
                filename, err
            ),
        }

        match super::settings_moby_runtime_uri(&mut check) {
            Ok(None) => panic!(
                "checking moby_runtime.uri in {} succeeded unexpectedly",
                filename
            ),

            Ok(Some(warning)) => assert!(
                warning.contains("is not supported for production"),
                "checking moby_runtime.uri in {} failed with an unexpected warning: {}",
                filename,
                warning
            ),

            Err(err) => panic!(
                "checking moby_runtime.uri in {} failed with an unexpected error: {}",
                filename, err
            ),
        }
    }
}
