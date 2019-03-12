// Copyright (c) Microsoft. All rights reserved.

use std;
use std::borrow::Cow;
use std::ffi::{CStr, OsStr, OsString};
use std::fs::File;
use std::io::{Read, Write};
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
use termcolor::WriteColor;

use edgelet_config::{Provisioning, Settings};
use edgelet_core::{self, UrlExt};
use edgelet_docker::DockerConfig;
use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;

use crate::error::{Error, ErrorKind, FetchLatestVersionsReason};
use crate::LatestVersions;

#[cfg(windows)]
const CONTAINER_RUNTIME_CONFIG_PATH: &str = r"C:\ProgramData\iotedge-moby\config\daemon.json";
#[cfg(unix)]
const CONTAINER_RUNTIME_CONFIG_PATH: &str = "/etc/docker/daemon.json";

pub struct Check {
    config_file: PathBuf,
    diagnostics_image_name: String,
    iotedged: PathBuf,
    latest_versions: Result<super::LatestVersions, Error>,
    ntp_server: String,
    verbose: bool,

    // These optional fields are populated by the pre-checks
    settings: Option<Settings<DockerConfig>>,
    docker_host_arg: Option<String>,
    docker_server_version: Option<String>,
    iothub_hostname: Option<String>,
}

#[derive(Debug)]
enum CheckResult {
    Ok,
    Warning(String),
    Skipped,
}

impl Check {
    pub fn new(
        config_file: PathBuf,
        diagnostics_image_name: String,
        expected_iotedged_version: Option<String>,
        iotedged: PathBuf,
        ntp_server: String,
        verbose: bool,
    ) -> impl Future<Item = Self, Error = Error> + Send {
        let latest_versions = if let Some(expected_iotedged_version) = expected_iotedged_version {
            future::Either::A(future::ok::<_, Error>(LatestVersions {
                iotedged: expected_iotedged_version,
            }))
        } else {
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

            future::Either::B(
                hyper_client
                    .and_then(|hyper_client| {
                        hyper_client.call(request).then(|response| {
                            let response = response.context(ErrorKind::FetchLatestVersions(
                                FetchLatestVersionsReason::GetResponse,
                            ))?;
                            Ok((response, hyper_client))
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
                            hyper::StatusCode::OK => {
                                Ok(response.into_body().concat2().map_err(|err| {
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
                        }
                    })
                    .flatten()
                    .and_then(|body| {
                        Ok(serde_json::from_slice(&body).context(
                            ErrorKind::FetchLatestVersions(FetchLatestVersionsReason::GetResponse),
                        )?)
                    }),
            )
        };

        latest_versions.then(move |latest_versions| {
            Ok(Check {
                config_file,
                diagnostics_image_name,
                iotedged,
                ntp_server,
                latest_versions,
                verbose,

                settings: None,
                docker_host_arg: None,
                docker_server_version: None,
                iothub_hostname: None,
            })
        })
    }

    fn execute_inner(&mut self) -> Result<(), Error> {
        const CHECKS: &[(
            &str, // Section name
            &[(
                &str,                                                  // Check description
                fn(&mut Check) -> Result<CheckResult, failure::Error>, // Check function
            )],
        )] = &[
            (
                "Configuration checks",
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
                    ("config.yaml has correct hostname", settings_hostname),
                    (
                        "config.yaml has correct URIs for daemon mgmt endpoint",
                        daemon_mgmt_endpoint_uri,
                    ),
                    ("latest security daemon", iotedged_version),
                    ("host time is close to real time", host_local_time),
                    ("container time is close to host time", container_local_time),
                    ("production readiness: certificates", settings_certificates),
                    (
                        "production readiness: certificates expiry",
                        settings_certificates_expiry,
                    ),
                    (
                        "production readiness: container engine",
                        settings_moby_runtime_uri,
                    ),
                    (
                        "production readiness: logs policy",
                        container_runtime_logrotate,
                    ),
                    ("production readiness: DNS server", container_runtime_dns),
                ],
            ),
            (
                "Connectivity checks",
                &[
                    (
                        "host can connect to and perform TLS handshake with IoT Hub AMQP port",
                        |check| connection_to_iot_hub_host(check, 5671),
                    ),
                    (
                        "host can connect to and perform TLS handshake with IoT Hub HTTPS port",
                        |check| connection_to_iot_hub_host(check, 443),
                    ),
                    (
                        "host can connect to and perform TLS handshake with IoT Hub MQTT port",
                        |check| connection_to_iot_hub_host(check, 8883),
                    ),
                    ("container on the default network can connect to IoT Hub AMQP port", |check| {
                        connection_to_iot_hub_container(check, 5671, false)
                    }),
                    ("container on the default network can connect to IoT Hub HTTPS port", |check| {
                        connection_to_iot_hub_container(check, 443, false)
                    }),
                    ("container on the default network can connect to IoT Hub MQTT port", |check| {
                        connection_to_iot_hub_container(check, 8883, false)
                    }),
                    ("container on the IoT Edge module network can connect to IoT Hub AMQP port", |check| {
                        connection_to_iot_hub_container(check, 5671, true)
                    }),
                    ("container on the IoT Edge module network can connect to IoT Hub HTTPS port", |check| {
                        connection_to_iot_hub_container(check, 443, true)
                    }),
                    ("container on the IoT Edge module network can connect to IoT Hub MQTT port", |check| {
                        connection_to_iot_hub_container(check, 8883, true)
                    }),
                    ("edge hub can bind to ports on host", edge_hub_ports_on_host),
                    (
                        "modules on the IoT Edge module network can resolve each other by name",
                        container_runtime_network,
                    ),
                ],
            ),
        ];

        let mut stdout = termcolor::StandardStream::stdout(termcolor::ColorChoice::Auto);
        let success_color_spec = {
            let mut success_color_spec = termcolor::ColorSpec::new();
            success_color_spec.set_fg(Some(termcolor::Color::Green));
            success_color_spec
        };
        let warning_color_spec = {
            let mut warning_color_spec = termcolor::ColorSpec::new();
            if cfg!(windows) {
                // `Color::Yellow` maps to `FOREGROUND_GREEN | FOREGROUND_RED` == 6 == `ConsoleColor::DarkYellow`.
                // In its default blue-background profile, PS uses `ConsoleColor::DarkYellow` as its default foreground text color
                // and maps it to a dark gray.
                //
                // So use explicit RGB to define yellow for Windows.
                //
                // Ref:
                // - https://docs.rs/termcolor/0.3.6/src/termcolor/lib.rs.html#1380 defines `termcolor::Color::Yellow` as `wincolor::Color::Yellow`
                // - https://docs.rs/wincolor/0.1.6/x86_64-pc-windows-msvc/src/wincolor/win.rs.html#18
                //   defines `wincolor::Color::Yellow` as `FG_YELLOW`, which in turn is `FOREGROUND_GREEN | FOREGROUND_RED`
                // - https://docs.microsoft.com/en-us/windows/console/char-info-str defines `FOREGROUND_GREEN | FOREGROUND_RED` as `2 | 4 == 6`
                // - https://docs.microsoft.com/en-us/dotnet/api/system.consolecolor#fields defines `6` as `[ConsoleColor]::DarkYellow`
                // - `$Host.UI.RawUI.ForegroundColor` in the default PS profile is `DarkYellow`, and writing in it prints dark gray text.
                warning_color_spec.set_fg(Some(termcolor::Color::Rgb(255, 255, 0)));
            } else {
                warning_color_spec.set_fg(Some(termcolor::Color::Yellow));
            }
            warning_color_spec
        };
        let error_color_spec = {
            let mut error_color_spec = termcolor::ColorSpec::new();
            error_color_spec.set_fg(Some(termcolor::Color::Red));
            error_color_spec
        };
        let is_a_tty = atty::is(atty::Stream::Stdout);

        let mut have_warnings = false;
        let mut have_skipped = false;
        let mut have_errors = false;

        for (section_name, section_checks) in CHECKS {
            println!("{}", section_name);
            println!("{}", "-".repeat(section_name.len()));

            for (check_name, check) in *section_checks {
                match check(self) {
                    Ok(CheckResult::Ok) => {
                        colored(&mut stdout, &success_color_spec, is_a_tty, |stdout| {
                            writeln!(stdout, "\u{221a} {}", check_name)?;
                            Ok(())
                        });
                    }

                    Ok(CheckResult::Warning(warning)) => {
                        have_warnings = true;

                        colored(&mut stdout, &warning_color_spec, is_a_tty, |stdout| {
                            writeln!(stdout, "\u{203c} {}", check_name)?;
                            writeln!(stdout, "    {}", warning)?;
                            Ok(())
                        });
                    }

                    Ok(CheckResult::Skipped) => {
                        have_skipped = true;

                        if self.verbose {
                            colored(&mut stdout, &warning_color_spec, is_a_tty, |stdout| {
                                writeln!(stdout, "\u{203c} {}", check_name)?;
                                writeln!(stdout, "    skipping because of previous failures")?;
                                Ok(())
                            });
                        }
                    }

                    Err(err) => {
                        have_errors = true;

                        colored(&mut stdout, &error_color_spec, is_a_tty, |stdout| {
                            writeln!(stdout, "\u{00d7} {}", check_name)?;

                            {
                                let err = err.to_string();
                                let mut lines = err.split('\n');
                                writeln!(stdout, "    {}", lines.next().unwrap())?;
                                for line in lines {
                                    writeln!(stdout, "    {}", line)?;
                                }
                            }

                            for cause in err.iter_causes() {
                                let cause = cause.to_string();
                                let mut lines = cause.split('\n');
                                writeln!(stdout, "        caused by: {}", lines.next().unwrap())?;
                                for line in lines {
                                    writeln!(stdout, "                   {}", line)?;
                                }
                            }

                            Ok(())
                        });
                    }
                }
            }

            println!();
        }

        match (have_warnings, have_skipped, have_errors) {
            (false, false, false) => {
                colored(&mut stdout, &success_color_spec, is_a_tty, |stdout| {
                    writeln!(stdout, "All checks succeeded")?;
                    Ok(())
                });

                Ok(())
            }

            (_, _, true) => {
                colored(&mut stdout, &error_color_spec, is_a_tty, |stdout| {
                    writeln!(stdout, "One or more checks raised errors")?;
                    Ok(())
                });

                Err(ErrorKind::Diagnostics.into())
            }

            (_, true, _) => {
                colored(&mut stdout, &warning_color_spec, is_a_tty, |stdout| {
                    writeln!(
                        stdout,
                        "One or more checks were skipped due to previous failures"
                    )?;
                    Ok(())
                });

                Ok(())
            }

            (true, _, _) => {
                colored(&mut stdout, &warning_color_spec, is_a_tty, |stdout| {
                    writeln!(stdout, "One or more checks raised warnings")?;
                    Ok(())
                });

                Ok(())
            }
        }
    }
}

impl crate::Command for Check {
    type Future = FutureResult<(), Error>;

    fn execute(&mut self) -> Self::Future {
        self.execute_inner().into_future()
    }
}

fn parse_settings(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let config_file = &check.config_file;

    // The config crate just returns a "file not found" error when it can't open the file for any reason,
    // even if the real error was a permissions issue.
    //
    // So we first try to open the file for reading ourselves.
    let _ = File::open(config_file).with_context(|err| match err.kind() {
        std::io::ErrorKind::PermissionDenied => format!(
            "Could not open {}. You might need to run this command as root.",
            config_file.display()
        ),
        _ => format!("could not open {}", config_file.display()),
    })?;

    let settings = Settings::new(Some(config_file))
        .with_context(|_| format!("could not parse {}", config_file.display()))?;

    check.settings = Some(settings);

    Ok(CheckResult::Ok)
}

fn settings_connection_string(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
    };

    if let Provisioning::Manual(manual) = settings.provisioning() {
        let (_, _, hub) = manual.parse_device_connection_string()?;
        check.iothub_hostname = Some(hub.to_string());
    }

    Ok(CheckResult::Ok)
}

fn container_runtime(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
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

    let output = docker(
        &docker_host_arg,
        &["version", "--format", "{{.Server.Version}}"],
    )?;
    if !output.status.success() {
        let mut err = format!(
            "docker returned {}, stderr = {}",
            output.status,
            String::from_utf8_lossy(&*output.stderr)
        );

        if err.contains("Got permission denied") {
            err += "\nYou might need to run this command as root.";
        }

        return Err(Context::new(err).into());
    }

    check.docker_host_arg = Some(docker_host_arg);

    check.docker_server_version = Some(String::from_utf8_lossy(&output.stdout).into_owned());

    Ok(CheckResult::Ok)
}

fn settings_hostname(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
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

    Ok(CheckResult::Ok)
}

fn daemon_mgmt_endpoint_uri(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
    };

    let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
        docker_host_arg
    } else {
        return Ok(CheckResult::Skipped);
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

        ("unix", "unix") | ("unix", "fd") => {
            args.push(Cow::Borrowed(OsStr::new("-v")));

            // On Windows we mount the parent folder because we can't mount the socket files directly

            let socket_path = connect_management_uri.to_uds_file_path().context("could not parse connect.management_uri as file path")?;
            #[cfg(windows)]
            let socket_path = socket_path.parent().ok_or_else(|| Context::new("connect.management_uri is not a valid file path - does not have a parent directory"))?;
            let socket_path = socket_path.to_str().ok_or_else(|| Context::new("connect.management_uri is a unix socket, but the file path is not valid utf-8"))?;

            args.push(Cow::Owned(format!("{}:{}", socket_path, socket_path).into()));
        },

        (scheme1, scheme2) if scheme1 != scheme2 => return Err(Context::new(
            format!("config.yaml has invalid combination of schemes for connect.management_uri ({:?}) and listen.management_uri ({:?})", scheme1, scheme2))
            .into()),

        (scheme, _) => return Err(Context::new(format!("unrecognized scheme {} for connect.management_uri", scheme)).into()),
    }

    args.extend(vec![
        Cow::Borrowed(OsStr::new(&check.diagnostics_image_name)),
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

    Ok(CheckResult::Ok)
}

fn iotedged_version(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let latest_versions = match &check.latest_versions {
        Ok(latest_versions) => latest_versions,
        Err(err) => return Ok(CheckResult::Warning(err.to_string())),
    };

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

    if version != latest_versions.iotedged {
        return Ok(CheckResult::Warning(format!(
            "expected iotedged to have version {} but it has version {}",
            latest_versions.iotedged, version
        )));
    }

    Ok(CheckResult::Ok)
}

fn host_local_time(check: &mut Check) -> Result<CheckResult, failure::Error> {
    fn is_server_unreachable_error(err: &mini_sntp::Error) -> bool {
        match err.kind() {
            mini_sntp::ErrorKind::ResolveNtpPoolHostname(_) => true,
            mini_sntp::ErrorKind::SendClientRequest(err)
            | mini_sntp::ErrorKind::ReceiveServerResponse(err) => {
                err.kind() == std::io::ErrorKind::TimedOut || // Windows
                err.kind() == std::io::ErrorKind::WouldBlock // Unix
            }
            _ => false,
        }
    }

    let mini_sntp::SntpTimeQueryResult {
        local_clock_offset, ..
    } = match mini_sntp::query(&check.ntp_server) {
        Ok(result) => result,
        Err(ref err) if is_server_unreachable_error(err) => {
            return Ok(CheckResult::Warning(err.to_string()));
        }
        Err(err) => return Err(err.into()),
    };

    if local_clock_offset.num_seconds().abs() >= 10 {
        return Ok(CheckResult::Warning(format!(
            "detected large difference between host local time and real time: {}",
            local_clock_offset
        )));
    }

    Ok(CheckResult::Ok)
}

fn container_local_time(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
        docker_host_arg
    } else {
        return Ok(CheckResult::Skipped);
    };

    let expected_duration = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .context("could not query local time of host")?;

    let output = docker(
        docker_host_arg,
        vec![
            "run",
            "--rm",
            &check.diagnostics_image_name,
            "/iotedge-diagnostics",
            "local-time",
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

    Ok(CheckResult::Ok)
}

fn settings_certificates(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
    };

    if settings.certificates().is_none() {
        Ok(CheckResult::Warning(
            "Certificates have not been set, so device will operate in quick start mode which is not supported in production"
                .to_string(),
        ))
    } else {
        Ok(CheckResult::Ok)
    }
}

fn settings_certificates_expiry(check: &mut Check) -> Result<CheckResult, failure::Error> {
    fn parse_openssl_time(
        time: &openssl::asn1::Asn1TimeRef,
    ) -> chrono::ParseResult<chrono::DateTime<chrono::Utc>> {
        // openssl::asn1::Asn1TimeRef does not expose any way to convert the ASN1_TIME to a Rust-friendly type
        //
        // Its Display impl uses ASN1_TIME_print, so we convert it into a String and parse it back
        // into a chrono::DateTime<chrono::Utc>
        let time = time.to_string();
        let time = chrono::NaiveDateTime::parse_from_str(&time, "%b %e %H:%M:%S %Y GMT")?;
        Ok(chrono::DateTime::<chrono::Utc>::from_utc(time, chrono::Utc))
    }

    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
    };

    let certificates = if let Some(certificates) = settings.certificates() {
        certificates
    } else {
        return Ok(CheckResult::Skipped);
    };

    let device_ca_cert_path = certificates.device_ca_cert();
    let mut device_ca_cert_file =
        File::open(device_ca_cert_path).context("could not parse certificates.device_ca_cert")?;
    let mut device_ca_cert = vec![];
    device_ca_cert_file
        .read_to_end(&mut device_ca_cert)
        .context("could not parse certificates.device_ca_cert")?;

    let device_ca_cert = openssl::x509::X509::stack_from_pem(&device_ca_cert)
        .context("could not parse certificates.device_ca_cert")?;
    let device_ca_cert = &device_ca_cert[0];

    let not_after = parse_openssl_time(device_ca_cert.not_after())
        .context("could not parse not-after time of certificates.device_ca_cert")?;
    let not_before = parse_openssl_time(device_ca_cert.not_before())
        .context("could not parse not-before time of certificates.device_ca_cert")?;

    let now = chrono::Utc::now();

    if not_before > now {
        return Err(Context::new(format!("certificate specified by certificates.device_ca_cert has not-before time {} which is in the future", not_before)).into());
    }

    if not_after < now {
        return Err(Context::new(format!(
            "certificate specified by certificates.device_ca_cert expired at {}",
            not_after
        ))
        .into());
    }

    if not_after < now + chrono::Duration::days(7) {
        return Ok(CheckResult::Warning(format!(
            "certificate specified by certificates.device_ca_cert will expire soon ({})",
            not_after
        )));
    }

    Ok(CheckResult::Ok)
}

fn settings_moby_runtime_uri(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
    };

    let docker_server_version = if let Some(docker_server_version) = &check.docker_server_version {
        docker_server_version
    } else {
        return Ok(CheckResult::Skipped);
    };

    if cfg!(windows) {
        let moby_runtime_uri = settings.moby_runtime().uri().to_string();

        if moby_runtime_uri != "npipe://./pipe/iotedge_moby_engine" {
            return Ok(CheckResult::Warning(format!(
                "moby_runtime.uri {:?} is not supported for production. \
                It must be set to \"npipe://./pipe/iotedge_moby_engine\" to use the supported Moby engine.",
                moby_runtime_uri
            )));
        }
    }

    let docker_server_major_version = docker_server_version
        .split('.')
        .next()
        .ok_or_else(|| {
            Context::new(format!(
                "container runtime returned malformed version string {:?}",
                docker_server_version
            ))
        })?
        .parse::<u32>()
        .with_context(|_| {
            format!(
                "container runtime returned malformed version string {:?}",
                docker_server_version
            )
        })?;

    // Moby does not identify itself in any unique way. Moby devs recommend assuming that anything less than version 10 is Moby,
    // since it's currently 3.x and regular Docker is in the high 10s.
    if docker_server_major_version >= 10 {
        return Ok(CheckResult::Warning("Container engine does not appear to be the Moby engine. Only the Moby engine is supported for production.".to_owned()));
    }

    Ok(CheckResult::Ok)
}

fn container_runtime_logrotate(_: &mut Check) -> Result<CheckResult, failure::Error> {
    #[derive(serde_derive::Deserialize)]
    struct DaemonConfig {
        #[serde(rename = "log-driver")]
        log_driver: Option<String>,

        #[serde(rename = "log-opts")]
        log_opts: Option<DaemonConfigLogOpts>,
    }

    #[derive(serde_derive::Deserialize)]
    struct DaemonConfigLogOpts {
        #[serde(rename = "max-file")]
        max_file: Option<String>,

        #[serde(rename = "max-size")]
        max_size: Option<String>,
    }

    let daemon_config_file = match File::open(CONTAINER_RUNTIME_CONFIG_PATH) {
        Ok(daemon_config_file) => daemon_config_file,
        Err(err) => {
            return Ok(CheckResult::Warning(format!(
                "could not open {}: {}",
                CONTAINER_RUNTIME_CONFIG_PATH, err
            )));
        }
    };

    let daemon_config: DaemonConfig = serde_json::from_reader(daemon_config_file)
        .with_context(|_| format!("could not parse {}", CONTAINER_RUNTIME_CONFIG_PATH))?;

    if daemon_config.log_driver.is_none() {
        return Ok(CheckResult::Warning(format!(
            "log-driver is not set in {}",
            CONTAINER_RUNTIME_CONFIG_PATH
        )));
    }

    if let Some(log_opts) = &daemon_config.log_opts {
        if log_opts.max_file.is_none() {
            return Ok(CheckResult::Warning(format!(
                "log-opts.max-file is not set in {}",
                CONTAINER_RUNTIME_CONFIG_PATH
            )));
        }

        if log_opts.max_size.is_none() {
            return Ok(CheckResult::Warning(format!(
                "log-opts.max-size is not set in {}",
                CONTAINER_RUNTIME_CONFIG_PATH
            )));
        }
    } else {
        return Ok(CheckResult::Warning(format!(
            "log-opts is not set in {}",
            CONTAINER_RUNTIME_CONFIG_PATH
        )));
    }

    Ok(CheckResult::Ok)
}

fn container_runtime_dns(_: &mut Check) -> Result<CheckResult, failure::Error> {
    #[derive(serde_derive::Deserialize)]
    struct DaemonConfig {
        dns: Option<Vec<String>>,
    }

    let daemon_config_file = match File::open(CONTAINER_RUNTIME_CONFIG_PATH) {
        Ok(daemon_config_file) => daemon_config_file,
        Err(err) => {
            return Ok(CheckResult::Warning(format!(
                "could not open {}: {}",
                CONTAINER_RUNTIME_CONFIG_PATH, err
            )));
        }
    };

    let daemon_config: DaemonConfig = serde_json::from_reader(daemon_config_file)
        .with_context(|_| format!("could not parse {}", CONTAINER_RUNTIME_CONFIG_PATH))?;

    if let Some(&[]) | None = daemon_config.dns.as_ref().map(std::ops::Deref::deref) {
        return Ok(CheckResult::Warning(format!(
            "No DNS servers are defined in {}",
            CONTAINER_RUNTIME_CONFIG_PATH
        )));
    }

    Ok(CheckResult::Ok)
}

fn connection_to_iot_hub_host(check: &mut Check, port: u16) -> Result<CheckResult, failure::Error> {
    let iothub_hostname = if let Some(iothub_hostname) = &check.iothub_hostname {
        iothub_hostname
    } else {
        return Ok(CheckResult::Skipped);
    };

    let iothub_host = std::net::ToSocketAddrs::to_socket_addrs(&(&**iothub_hostname, port))
        .with_context(|_| "could not resolve Azure IoT Hub hostname")?
        .next()
        .ok_or_else(|| {
            Context::new("could not resolve Azure IoT Hub hostname: no addresses found")
        })?;

    let stream = TcpStream::connect_timeout(&iothub_host, std::time::Duration::from_secs(10))
        .context("could not connect to IoT Hub")?;

    let tls_connector =
        native_tls::TlsConnector::new().context("could not create TLS connector")?;

    let _ = tls_connector
        .connect(iothub_hostname, stream)
        .context("could not complete TLS handshake with Azure IoT Hub")?;

    Ok(CheckResult::Ok)
}

fn connection_to_iot_hub_container(
    check: &mut Check,
    port: u16,
    use_container_runtime_network: bool,
) -> Result<CheckResult, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
    };

    let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
        docker_host_arg
    } else {
        return Ok(CheckResult::Skipped);
    };

    let iothub_hostname = if let Some(iothub_hostname) = &check.iothub_hostname {
        iothub_hostname
    } else {
        return Ok(CheckResult::Skipped);
    };

    let network_name = settings.moby_runtime().network();

    let port = port.to_string();

    let mut args = vec!["run", "--rm"];

    if use_container_runtime_network {
        args.extend(&["--network", network_name]);
    }

    args.extend(&[
        &check.diagnostics_image_name,
        "/iotedge-diagnostics",
        "iothub",
        "--hostname",
        iothub_hostname,
        "--port",
        &port,
    ]);

    let output = docker(docker_host_arg, args)?;
    if !output.status.success() {
        return Err(Context::new(format!(
            "container on the {} network could not connect to Azure IoT Hub\n\
             docker returned {}, stderr = {}",
            if use_container_runtime_network {
                network_name
            } else {
                "default"
            },
            output.status,
            String::from_utf8_lossy(&*output.stderr)
        ))
        .into());
    }

    Ok(CheckResult::Ok)
}

fn edge_hub_ports_on_host(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
        docker_host_arg
    } else {
        return Ok(CheckResult::Skipped);
    };

    let output = docker(docker_host_arg, vec!["inspect", "edgeHub"])?;
    if !output.status.success() {
        return Err(Context::new(format!(
            "docker returned {}, stderr = {}",
            output.status,
            String::from_utf8_lossy(&*output.stderr),
        ))
        .into());
    }

    let (inspect_result,): (docker::models::InlineResponse200,) =
        serde_json::from_slice(&output.stdout)
            .context("could not parse result of docker inspect")?;

    let is_running = inspect_result
        .state()
        .and_then(docker::models::InlineResponse200State::running)
        .cloned()
        .ok_or_else(|| {
            Context::new("could not parse result of docker inspect: state.status is not set")
        })?;
    if is_running {
        // Whatever ports it wanted to bind to must've been available for it to be running
        return Ok(CheckResult::Ok);
    }

    let port_bindings = inspect_result
        .host_config()
        .and_then(docker::models::HostConfig::port_bindings)
        .ok_or_else(|| {
            Context::new(
                "could not parse result of docker inspect: host_config.port_bindings is not set",
            )
        })?
        .values()
        .flatten()
        .filter_map(docker::models::HostConfigPortBindings::host_port);

    for port_binding in port_bindings {
        // Try to bind to the port ourselves. If it fails with AddrInUse, then something else has bound to it.
        match std::net::TcpListener::bind(format!("127.0.0.1:{}", port_binding)) {
            Ok(_) => (),
            Err(ref err) if err.kind() == std::io::ErrorKind::AddrInUse => {
                return Err(Context::new(format!(
                    "port {} is not available for edge hub to bind to",
                    port_binding
                ))
                .into());
            }
            Err(err) => {
                return Err(err
                    .context(format!(
                        "could not check if port {} is available for edge hub to bind to",
                        port_binding
                    ))
                    .into());
            }
        }
    }

    Ok(CheckResult::Ok)
}

fn container_runtime_network(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
    };

    let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
        docker_host_arg
    } else {
        return Ok(CheckResult::Skipped);
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
    module1_process.arg(&check.diagnostics_image_name);
    module1_process.args(vec![
        "/iotedge-diagnostics",
        "idle-module",
        "--duration",
        "10",
    ]);
    module1_process.stdout(std::process::Stdio::null());
    module1_process.stderr(std::process::Stdio::null());

    // Let it run in the background
    module1_process
        .spawn()
        .with_context(|_| format!("could not run {:?}", module1_process))?;

    let module2_output = docker(
        docker_host_arg,
        vec![
            "run",
            "--rm",
            "--network",
            network_name,
            "--name",
            "diagnostics-2",
            &check.diagnostics_image_name,
            "/iotedge-diagnostics",
            "resolve-module",
            "--hostname",
            "diagnostics-1",
        ],
    )?;
    if !module2_output.status.success() {
        return Ok(CheckResult::Warning(format!(
            "docker returned {}, stderr = {}",
            module2_output.status,
            String::from_utf8_lossy(&*module2_output.stderr)
        )));
    }

    Ok(CheckResult::Ok)
}

fn colored<F>(
    stdout: &mut termcolor::StandardStream,
    spec: &termcolor::ColorSpec,
    is_a_tty: bool,
    f: F,
) where
    F: FnOnce(&mut termcolor::StandardStream) -> std::io::Result<()>,
{
    if is_a_tty {
        let _ = stdout.set_color(spec);
    }

    f(stdout).expect("could not write to stdout");

    if is_a_tty {
        let _ = stdout.reset();
    }
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
                    "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                    Some("1.0.0".to_owned()),      // unused for this test
                    "iotedged".into(),             // unused for this test
                    "pool.ntp.org:123".to_owned(), // unused for this test
                    false,
                ))
                .unwrap();

            match super::parse_settings(&mut check) {
                Ok(super::CheckResult::Ok) => (),
                check_result => panic!("parsing {} returned {:?}", filename, check_result),
            }

            match super::settings_connection_string(&mut check) {
                Ok(super::CheckResult::Ok) => (),
                check_result => panic!(
                    "checking connection string in {} returned {:?}",
                    filename, check_result
                ),
            }

            match super::settings_hostname(&mut check) {
                Ok(check_result) => panic!(
                    "checking hostname in {} returned {:?}",
                    filename, check_result
                ),
                Err(err) => assert!(
                    err.to_string()
                        .contains("but config has hostname localhost"),
                    "checking hostname in {} produced unexpected error: {}",
                    filename,
                    err,
                ),
            }

            // Pretend it's Moby
            check.docker_server_version = Some("3.0.3".to_string());

            match super::settings_moby_runtime_uri(&mut check) {
                Ok(super::CheckResult::Ok) => (),
                check_result => panic!(
                    "checking moby_runtime.uri in {} returned {:?}",
                    filename, check_result
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
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Some("1.0.0".to_owned()),      // unused for this test
                "iotedged".into(),             // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                false,
            ))
            .unwrap();

        match super::parse_settings(&mut check) {
            Ok(check_result) => panic!("parsing {} returned {:?}", filename, check_result),
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
    fn moby_runtime_uri_windows_wants_moby_based_on_runtime_uri() {
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
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Some("1.0.0".to_owned()),      // unused for this test
                "iotedged".into(),             // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                false,
            ))
            .unwrap();

        match super::parse_settings(&mut check) {
            Ok(super::CheckResult::Ok) => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }

        // Pretend it's Moby
        check.docker_server_version = Some("3.0.3".to_string());

        match super::settings_moby_runtime_uri(&mut check) {
            Ok(super::CheckResult::Warning(warning)) => assert!(
                warning.contains(r#"It must be set to "npipe://./pipe/iotedge_moby_engine" to use the supported Moby engine"#),
                "checking moby_runtime.uri in {} failed with an unexpected warning: {}",
                filename,
                warning
            ),

            check_result => panic!(
                "checking moby_runtime.uri in {} returned {:?}",
                filename, check_result
            ),
        }
    }

    #[test]
    fn moby_runtime_uri_wants_moby_based_on_server_version() {
        let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();

        let filename = "sample_settings.yaml";
        let config_file = format!(
            "{}/../edgelet-config/test/{}/{}",
            env!("CARGO_MANIFEST_DIR"),
            if cfg!(windows) { "windows" } else { "linux" },
            filename
        );

        let mut check = runtime
            .block_on(super::Check::new(
                config_file.into(),
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Some("1.0.0".to_owned()),      // unused for this test
                "iotedged".into(),             // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                false,
            ))
            .unwrap();

        match super::parse_settings(&mut check) {
            Ok(super::CheckResult::Ok) => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }

        // Pretend it's Docker
        check.docker_server_version = Some("18.09.1".to_string());

        match super::settings_moby_runtime_uri(&mut check) {
            Ok(super::CheckResult::Warning(warning)) => assert!(
                warning.contains("Container engine does not appear to be the Moby engine."),
                "checking moby_runtime.uri in {} failed with an unexpected warning: {}",
                filename,
                warning
            ),

            check_result => panic!(
                "checking moby_runtime.uri in {} returned {:?}",
                filename, check_result
            ),
        }
    }
}
