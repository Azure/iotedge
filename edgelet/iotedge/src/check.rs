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
use edgelet_core::UrlExt;
use edgelet_docker::DockerConfig;
use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;

use error::{Error, ErrorKind, FetchLatestVersionsReason};
use LatestVersions;

pub struct Check {
    config_file: PathBuf,
    container_engine_config_path: PathBuf,
    diagnostics_image_name: String,
    iotedged: PathBuf,
    latest_versions: Result<super::LatestVersions, Option<Error>>,
    ntp_server: String,
    verbose: bool,

    // These optional fields are populated by the pre-checks
    settings: Option<Settings<DockerConfig>>,
    docker_host_arg: Option<String>,
    docker_server_version: Option<String>,
    iothub_hostname: Option<String>,
}

/// The various ways a check can resolve.
///
/// Check functions return `Result<CheckResult, failure::Error>` where `Err` represents the check failed.
#[derive(Debug)]
enum CheckResult {
    /// Check succeeded.
    Ok,

    /// Check failed with a warning.
    Warning(failure::Error),

    /// Check is not applicable and was ignored. Should be treated as success.
    Ignored,

    /// Check was skipped because of errors from some previous checks. Should be treated as an error.
    Skipped,

    /// Check failed, and further checks should not be performed.
    Fatal(failure::Error),
}

impl Check {
    pub fn new(
        config_file: PathBuf,
        container_engine_config_path: PathBuf,
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
                container_engine_config_path,
                diagnostics_image_name,
                iotedged,
                ntp_server,
                latest_versions: latest_versions.map_err(Some),
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
                        "container engine is installed and functional",
                        container_engine,
                    ),
                    ("config.yaml has correct hostname", settings_hostname),
                    (
                        "config.yaml has correct URIs for daemon mgmt endpoint",
                        daemon_mgmt_endpoint_uri,
                    ),
                    ("latest security daemon", iotedged_version),
                    ("host time is close to real time", host_local_time),
                    ("container time is close to host time", container_local_time),
                    ("DNS server", container_engine_dns),
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
                        container_engine_logrotate,
                    ),
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
                        if cfg!(windows) {
                            // The default network is the same as the IoT Edge module network,
                            // so let the module network checks handle it.
                            Ok(CheckResult::Ignored)
                        } else {
                            connection_to_iot_hub_container(check, 5671, false)
                        }
                    }),
                    ("container on the default network can connect to IoT Hub HTTPS port", |check| {
                        if cfg!(windows) {
                            // The default network is the same as the IoT Edge module network,
                            // so let the module network checks handle it.
                            Ok(CheckResult::Ignored)
                        } else {
                            connection_to_iot_hub_container(check, 443, false)
                        }
                    }),
                    ("container on the default network can connect to IoT Hub MQTT port", |check| {
                        if cfg!(windows) {
                            // The default network is the same as the IoT Edge module network,
                            // so let the module network checks handle it.
                            Ok(CheckResult::Ignored)
                        } else {
                            connection_to_iot_hub_container(check, 8883, false)
                        }
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
                    ("Edge Hub can bind to ports on host", edge_hub_ports_on_host),
                ],
            ),
        ];

        let mut stdout = termcolor::StandardStream::stdout(termcolor::ColorChoice::Auto);
        let success_color_spec = {
            let mut success_color_spec = termcolor::ColorSpec::new();
            if cfg!(windows) {
                // `Color::Green` maps to `FG_GREEN` which is too hard to read on the default blue-background profile that PS uses.
                // PS uses `FG_GREEN | FG_INTENSITY` == 8 == `[ConsoleColor]::Green` as the foreground color for its error text,
                // so mimic that.
                success_color_spec.set_fg(Some(termcolor::Color::Rgb(0, 255, 0)));
            } else {
                success_color_spec.set_fg(Some(termcolor::Color::Green));
            }
            success_color_spec
        };
        let warning_color_spec = {
            let mut warning_color_spec = termcolor::ColorSpec::new();
            if cfg!(windows) {
                // `Color::Yellow` maps to `FOREGROUND_GREEN | FOREGROUND_RED` == 6 == `ConsoleColor::DarkYellow`.
                // In its default blue-background profile, PS uses `ConsoleColor::DarkYellow` as its default foreground text color
                // and maps it to a dark gray.
                //
                // So use explicit RGB to define yellow for Windows. Also use a black background to mimic PS warnings.
                //
                // Ref:
                // - https://docs.rs/termcolor/0.3.6/src/termcolor/lib.rs.html#1380 defines `termcolor::Color::Yellow` as `wincolor::Color::Yellow`
                // - https://docs.rs/wincolor/0.1.6/x86_64-pc-windows-msvc/src/wincolor/win.rs.html#18
                //   defines `wincolor::Color::Yellow` as `FG_YELLOW`, which in turn is `FOREGROUND_GREEN | FOREGROUND_RED`
                // - https://docs.microsoft.com/en-us/windows/console/char-info-str defines `FOREGROUND_GREEN | FOREGROUND_RED` as `2 | 4 == 6`
                // - https://docs.microsoft.com/en-us/dotnet/api/system.consolecolor#fields defines `6` as `[ConsoleColor]::DarkYellow`
                // - `$Host.UI.RawUI.ForegroundColor` in the default PS profile is `DarkYellow`, and writing in it prints dark gray text.
                warning_color_spec.set_fg(Some(termcolor::Color::Rgb(255, 255, 0)));
                warning_color_spec.set_bg(Some(termcolor::Color::Black));
            } else {
                warning_color_spec.set_fg(Some(termcolor::Color::Yellow));
            }
            warning_color_spec
        };
        let error_color_spec = {
            let mut error_color_spec = termcolor::ColorSpec::new();
            if cfg!(windows) {
                // `Color::Red` maps to `FG_RED` which is too hard to read on the default blue-background profile that PS uses.
                // PS uses `FG_RED | FG_INTENSITY` == 12 == `[ConsoleColor]::Red` as the foreground color for its error text,
                // with black background, so mimic that.
                error_color_spec.set_fg(Some(termcolor::Color::Rgb(255, 0, 0)));
                error_color_spec.set_bg(Some(termcolor::Color::Black));
            } else {
                error_color_spec.set_fg(Some(termcolor::Color::Red));
            }
            error_color_spec
        };
        let is_a_tty = atty::is(atty::Stream::Stdout);

        let mut have_warnings = false;
        let mut have_skipped = false;
        let mut have_fatal = false;
        let mut have_errors = false;

        for (section_name, section_checks) in CHECKS {
            if have_fatal {
                break;
            }

            println!("{}", section_name);
            println!("{}", "-".repeat(section_name.len()));

            for (check_name, check) in *section_checks {
                if have_fatal {
                    break;
                }

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

                            let message = warning.to_string();

                            write_lines(stdout, "    ", "    ", message.lines())?;

                            if self.verbose {
                                for cause in warning.iter_causes() {
                                    write_lines(
                                        stdout,
                                        "        caused by: ",
                                        "                   ",
                                        cause.to_string().lines(),
                                    )?;
                                }
                            }

                            Ok(())
                        });
                    }

                    Ok(CheckResult::Ignored) => (),

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

                    Ok(CheckResult::Fatal(err)) => {
                        have_fatal = true;

                        colored(&mut stdout, &error_color_spec, is_a_tty, |stdout| {
                            writeln!(stdout, "\u{00d7} {}", check_name)?;

                            let message = err.to_string();

                            write_lines(stdout, "    ", "    ", message.lines())?;

                            if self.verbose {
                                for cause in err.iter_causes() {
                                    write_lines(
                                        stdout,
                                        "        caused by: ",
                                        "                   ",
                                        cause.to_string().lines(),
                                    )?;
                                }
                            }

                            Ok(())
                        });
                    }

                    Err(err) => {
                        have_errors = true;

                        colored(&mut stdout, &error_color_spec, is_a_tty, |stdout| {
                            writeln!(stdout, "\u{00d7} {}", check_name)?;

                            let message = err.to_string();

                            write_lines(stdout, "    ", "    ", message.lines())?;

                            if self.verbose {
                                for cause in err.iter_causes() {
                                    write_lines(
                                        stdout,
                                        "        caused by: ",
                                        "                   ",
                                        cause.to_string().lines(),
                                    )?;
                                }
                            }

                            Ok(())
                        });
                    }
                }
            }

            println!();
        }

        match (have_warnings, have_skipped, have_fatal || have_errors) {
            (false, false, false) => {
                colored(&mut stdout, &success_color_spec, is_a_tty, |stdout| {
                    writeln!(stdout, "All checks succeeded.")?;
                    Ok(())
                });

                Ok(())
            }

            (_, _, true) => {
                colored(&mut stdout, &error_color_spec, is_a_tty, |stdout| {
                    write!(stdout, "One or more checks raised errors.")?;
                    if self.verbose {
                        writeln!(stdout)?;
                    } else {
                        writeln!(stdout, " Re-run with --verbose for more details.")?;
                    }
                    Ok(())
                });

                Err(ErrorKind::Diagnostics.into())
            }

            (_, true, _) => {
                colored(&mut stdout, &warning_color_spec, is_a_tty, |stdout| {
                    write!(
                        stdout,
                        "One or more checks were skipped due to errors from other checks."
                    )?;
                    if self.verbose {
                        writeln!(stdout)?;
                    } else {
                        writeln!(stdout, " Re-run with --verbose for more details.")?;
                    }
                    Ok(())
                });

                Ok(())
            }

            (true, _, _) => {
                colored(&mut stdout, &warning_color_spec, is_a_tty, |stdout| {
                    write!(stdout, "One or more checks raised warnings.")?;
                    if self.verbose {
                        writeln!(stdout)?;
                    } else {
                        writeln!(stdout, " Re-run with --verbose for more details.")?;
                    }
                    Ok(())
                });

                Ok(())
            }
        }
    }
}

impl ::Command for Check {
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
    if let Err(err) = File::open(config_file) {
        if err.kind() == std::io::ErrorKind::PermissionDenied {
            return Ok(CheckResult::Fatal(
                err.context(format!(
                    "Could not open file {}. You might need to run this command as {}.",
                    config_file.display(),
                    if cfg!(windows) {
                        "Administrator"
                    } else {
                        "root"
                    },
                ))
                .into(),
            ));
        } else {
            return Err(err
                .context(format!("Could not open file {}", config_file.display()))
                .into());
        }
    }

    let settings = match Settings::new(Some(config_file)) {
        Ok(settings) => settings,
        Err(err) => {
            let message = if check.verbose {
                format!(
                    "The IoT Edge daemon's configuration file {} is not well-formed.\n\
                     Note: In case of syntax errors, the error may not be exactly at the reported line number and position.",
                    config_file.display(),
                )
            } else {
                format!(
                    "The IoT Edge daemon's configuration file {} is not well-formed.",
                    config_file.display(),
                )
            };
            return Err(err.context(message).into());
        }
    };

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
        let (_, _, hub) = manual.parse_device_connection_string().context(
            "Invalid connection string format detected.\n\
             Please check the value of the provisioning.device_connection_string parameter.",
        )?;
        check.iothub_hostname = Some(hub.to_owned());
    }

    Ok(CheckResult::Ok)
}

fn container_engine(check: &mut Check) -> Result<CheckResult, failure::Error> {
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
                "Could not communicate with container engine at {}. The scheme {} is invalid.",
                uri, scheme,
            ))
            .into());
        }
    };

    let output = docker(
        &docker_host_arg,
        &["version", "--format", "{{.Server.Version}}"],
    );
    let output = match output {
        Ok(output) => output,
        Err((message, err)) => {
            let mut error_message = format!(
                "Could not communicate with container engine at {}.\n\
                 Please check your moby-engine installation and ensure the service is running.",
                uri,
            );

            if let Some(message) = message {
                #[cfg(unix)]
                {
                    if message.contains("Got permission denied") {
                        error_message += "\nYou might need to run this command as root.";
                        return Ok(CheckResult::Fatal(err.context(error_message).into()));
                    }
                }

                #[cfg(windows)]
                {
                    if message.contains("Access is denied") {
                        error_message += "\nYou might need to run this command as Administrator.";
                        return Ok(CheckResult::Fatal(err.context(error_message).into()));
                    }
                }
            }

            return Err(err.context(error_message).into());
        }
    };

    check.docker_host_arg = Some(docker_host_arg);

    check.docker_server_version = Some(String::from_utf8_lossy(&output).into_owned());

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
                return Err(
                    std::io::Error::last_os_error() // Calls errno
                        .context("Could not get hostname: gethostname failed")
                        .into(),
                );
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
                    return Err(Context::new(format!(
                        "Could not get hostname: WSAStartup failed with {}",
                        result,
                    ))
                    .into());
                }
            }

            if winapi::um::winsock2::gethostname(result.as_mut_ptr() as _, result.len() as _) != 0 {
                // Can't use std::io::Error::last_os_error() because that calls GetLastError, not WSAGetLastError
                let winsock_err = winapi::um::winsock2::WSAGetLastError();

                return Err(Context::new(format!(
                    "Could not get hostname: gethostname failed with {}",
                    winsock_err,
                ))
                .into());
            }
        }

        let nul_index = result.iter().position(|&b| b == b'\0').ok_or_else(|| {
            Context::new("Could not get hostname: gethostname did not return NUL-terminated string")
        })?;

        CStr::from_bytes_with_nul_unchecked(&result[..=nul_index])
            .to_str()
            .context("Could not get hostname: gethostname returned non-ASCII string")?
            .to_owned()
    };

    if config_hostname != machine_hostname {
        return Err(Context::new(format!(
            "config.yaml has hostname {} but device reports hostname {}",
            config_hostname, machine_hostname,
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

            let socket_path =
                connect_management_uri.to_uds_file_path()
                .context("Could not parse connect.management_uri: does not represent a valid file path")?;

            // On Windows we mount the parent folder because we can't mount the socket files directly
            #[cfg(windows)]
            let socket_path =
                socket_path.parent()
                .ok_or_else(|| Context::new("Could not parse connect.management_uri: does not have a parent directory"))?;

            let socket_path =
                socket_path.to_str()
                .ok_or_else(|| Context::new("Could not parse connect.management_uri: file path is not valid utf-8"))?;

            args.push(Cow::Owned(format!("{}:{}", socket_path, socket_path).into()));
        },

        (scheme1, scheme2) if scheme1 != scheme2 => return Err(Context::new(
            format!(
                "config.yaml has invalid combination of schemes for connect.management_uri ({:?}) and listen.management_uri ({:?})",
                scheme1, scheme2,
            ))
            .into()),

        (scheme, _) => return Err(Context::new(
            format!("Could not parse connect.management_uri: scheme {} is invalid", scheme),
        ).into()),
    }

    args.extend(vec![
        Cow::Borrowed(OsStr::new(&check.diagnostics_image_name)),
        Cow::Borrowed(OsStr::new("/iotedge-diagnostics")),
        Cow::Borrowed(OsStr::new("edge-agent")),
        Cow::Borrowed(OsStr::new("--management-uri")),
        Cow::Owned(OsString::from(connect_management_uri.to_string())),
    ]);

    match docker(docker_host_arg, args) {
        Ok(_) => Ok(CheckResult::Ok),
        Err((Some(stderr), err)) => Err(err.context(stderr).into()),
        Err((None, err)) => Err(err.context("Could not spawn docker process").into()),
    }
}

fn iotedged_version(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let latest_versions = match &mut check.latest_versions {
        Ok(latest_versions) => &*latest_versions,
        Err(err) => match err.take() {
            Some(err) => return Ok(CheckResult::Warning(err.into())),
            None => return Ok(CheckResult::Skipped),
        },
    };

    let mut process = Command::new(&check.iotedged);
    process.arg("--version");

    if cfg!(windows) {
        process.env("IOTEDGE_RUN_AS_CONSOLE", "true");
    }

    let output = process
        .output()
        .context("Could not spawn iotedged process")?;
    if !output.status.success() {
        return Err(Context::new(format!(
            "iotedged returned {}, stderr = {}",
            output.status,
            String::from_utf8_lossy(&*output.stderr),
        ))
        .context("Could not spawn iotedged process")
        .into());
    }

    let output =
        String::from_utf8(output.stdout).context("Could not parse output of iotedged --version")?;

    let iotedged_version_regex = Regex::new(r"^iotedged ([^ ]+)(?: \(.*\))?$")
        .expect("This hard-coded regex is expected to be valid.");
    let captures = iotedged_version_regex
        .captures(output.trim())
        .ok_or_else(|| {
            Context::new(format!(
                "output {:?} does not match expected format",
                output,
            ))
            .context("Could not parse output of iotedged --version")
        })?;
    let version = captures
        .get(1)
        .expect("unreachable: regex defines one capturing group")
        .as_str();

    if version != latest_versions.iotedged {
        return Ok(CheckResult::Warning(
            Context::new(format!(
                "Installed IoT Edge daemon has version {} but version {} is available.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                version, latest_versions.iotedged,
            ))
            .into(),
        ));
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
        Err(err) => {
            if is_server_unreachable_error(&err) {
                return Ok(CheckResult::Warning(
                    err.context("Could not query NTP server").into(),
                ));
            } else {
                return Err(err.context("Could not query NTP server").into());
            }
        }
    };

    if local_clock_offset.num_seconds().abs() >= 10 {
        return Ok(CheckResult::Warning(Context::new(format!(
            "Time on the device is out of sync with the NTP server. This may cause problems connecting to IoT Hub.\n\
             Please ensure time on device is accurate, for example by {}.",
            if cfg!(windows) {
                "setting up the Windows Time service to automatically sync with a time server"
            } else {
                "installing an NTP daemon"
            },
        )).into()));
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
        .context("Could not query local time of host")?;

    let output = docker(
        docker_host_arg,
        vec![
            "run",
            "--rm",
            &check.diagnostics_image_name,
            "/iotedge-diagnostics",
            "local-time",
        ],
    )
    .map_err(|(_, err)| err)
    .context("Could not query local time inside container")?;
    let output = std::str::from_utf8(&output)
        .map_err(failure::Error::from)
        .and_then(|output| output.trim_end().parse::<u64>().map_err(Into::into))
        .context("Could not parse container output")?;
    let actual_duration = std::time::Duration::from_secs(output);

    let diff = std::cmp::max(actual_duration, expected_duration)
        - std::cmp::min(actual_duration, expected_duration);
    if diff.as_secs() >= 10 {
        return Err(Context::new("Detected time drift between host and container").into());
    }

    Ok(CheckResult::Ok)
}

fn container_engine_dns(check: &mut Check) -> Result<CheckResult, failure::Error> {
    const MESSAGE: &str =
        "Container engine is not configured with DNS server setting, which may impact connectivity to IoT Hub.\n\
         Please see https://aka.ms/iotedge-prod-checklist-dns for best practices.\n\
         You can ignore this warning if you are setting DNS server per module in the Edge deployment.";

    #[derive(serde_derive::Deserialize)]
    struct DaemonConfig {
        dns: Option<Vec<String>>,
    }

    let daemon_config_file = File::open(&check.container_engine_config_path)
        .with_context(|_| {
            format!(
                "Could not open container engine config file {}",
                check.container_engine_config_path.display(),
            )
        })
        .context(MESSAGE);
    let daemon_config_file = match daemon_config_file {
        Ok(daemon_config_file) => daemon_config_file,
        Err(err) => {
            return Ok(CheckResult::Warning(err.into()));
        }
    };
    let daemon_config: DaemonConfig = serde_json::from_reader(daemon_config_file)
        .with_context(|_| {
            format!(
                "Could not parse container engine config file {}",
                check.container_engine_config_path.display(),
            )
        })
        .context(MESSAGE)?;

    if let Some(&[]) | None = daemon_config.dns.as_ref().map(std::ops::Deref::deref) {
        return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
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
        return Ok(CheckResult::Warning(
            Context::new(
                "Device is using self-signed, automatically generated certs.\n\
                 Please see https://aka.ms/iotedge-prod-checklist-certs for best practices.",
            )
            .into(),
        ));
    }

    Ok(CheckResult::Ok)
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

    let (device_ca_cert_path, device_ca_cert_path_source) = if let Some(certificates) =
        settings.certificates()
    {
        (
            certificates.device_ca_cert().to_owned(),
            Cow::Borrowed("certificates.device_ca_cert"),
        )
    } else {
        let certs_dir = settings.homedir().join("hsm").join("certs");

        let mut device_ca_cert_path = None;

        let entries = std::fs::read_dir(&certs_dir)
            .with_context(|_| format!("Could not enumerate files under {}", certs_dir.display()))?;
        for entry in entries {
            let entry = entry.with_context(|_| {
                format!("Could not enumerate files under {}", certs_dir.display())
            })?;
            let path = entry.path();
            let is_device_ca_cert =
                if let Some(file_name) = path.file_name().and_then(OsStr::to_str) {
                    file_name.starts_with("device_ca_alias") && file_name.ends_with(".cert.pem")
                } else {
                    false
                };
            if is_device_ca_cert {
                device_ca_cert_path = Some(path);
                break;
            }
        }

        let device_ca_cert_path = device_ca_cert_path.ok_or_else(|| {
            Context::new(format!(
                "Could not find device CA certificate under {}",
                certs_dir.display(),
            ))
        })?;
        let device_ca_cert_path_source = device_ca_cert_path.to_string_lossy().into_owned();
        (device_ca_cert_path, Cow::Owned(device_ca_cert_path_source))
    };

    let (not_after, not_before) = File::open(device_ca_cert_path)
        .map_err(failure::Error::from)
        .and_then(|mut device_ca_cert_file| {
            let mut device_ca_cert = vec![];
            device_ca_cert_file.read_to_end(&mut device_ca_cert)?;
            let device_ca_cert = openssl::x509::X509::stack_from_pem(&device_ca_cert)?;
            let device_ca_cert = &device_ca_cert[0];

            let not_after = parse_openssl_time(device_ca_cert.not_after())?;
            let not_before = parse_openssl_time(device_ca_cert.not_before())?;

            Ok((not_after, not_before))
        })
        .with_context(|_| {
            format!(
                "Could not parse {} as a valid certificate file",
                device_ca_cert_path_source,
            )
        })?;

    let now = chrono::Utc::now();

    if not_before > now {
        return Err(Context::new(format!(
            "Device CA certificate in {} has not-before time {} which is in the future",
            device_ca_cert_path_source, not_before,
        ))
        .into());
    }

    if not_after < now {
        return Err(Context::new(format!(
            "Device CA certificate in {} expired at {}",
            device_ca_cert_path_source, not_after,
        ))
        .into());
    }

    if not_after < now + chrono::Duration::days(7) {
        return Ok(CheckResult::Warning(
            Context::new(format!(
                "Device CA certificate in {} will expire soon ({})",
                device_ca_cert_path_source, not_after,
            ))
            .into(),
        ));
    }

    Ok(CheckResult::Ok)
}

fn settings_moby_runtime_uri(check: &mut Check) -> Result<CheckResult, failure::Error> {
    const MESSAGE: &str =
        "Device is not using a production-supported container engine (moby-engine).\n\
         Please see https://aka.ms/iotedge-prod-checklist-moby for details.";

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
            return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
        }
    }

    let docker_server_major_version = docker_server_version
        .split('.')
        .next()
        .map(std::str::FromStr::from_str);
    let docker_server_major_version: u32 = match docker_server_major_version {
        Some(Ok(docker_server_major_version)) => docker_server_major_version,
        Some(Err(_)) | None => {
            return Ok(CheckResult::Warning(
                Context::new(format!(
                    "Container engine returned malformed version string {:?}",
                    docker_server_version,
                ))
                .context(MESSAGE)
                .into(),
            ));
        }
    };

    // Moby does not identify itself in any unique way. Moby devs recommend assuming that anything less than version 10 is Moby,
    // since it's currently 3.x and regular Docker is in the high 10s.
    if docker_server_major_version >= 10 {
        return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
    }

    Ok(CheckResult::Ok)
}

fn container_engine_logrotate(check: &mut Check) -> Result<CheckResult, failure::Error> {
    const MESSAGE: &str =
        "Container engine is not configured to rotate module logs which may cause it run out of disk space.\n\
         Please see https://aka.ms/iotedge-prod-checklist-logs for best practices.\n\
         You can ignore this warning if you are setting log policy per module in the Edge deployment.";

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

    let daemon_config_file = File::open(&check.container_engine_config_path)
        .with_context(|_| {
            format!(
                "Could not open container engine config file {}",
                check.container_engine_config_path.display(),
            )
        })
        .context(MESSAGE);
    let daemon_config_file = match daemon_config_file {
        Ok(daemon_config_file) => daemon_config_file,
        Err(err) => {
            return Ok(CheckResult::Warning(err.into()));
        }
    };
    let daemon_config: DaemonConfig = serde_json::from_reader(daemon_config_file)
        .with_context(|_| {
            format!(
                "Could not parse container engine config file {}",
                check.container_engine_config_path.display(),
            )
        })
        .context(MESSAGE)?;

    if daemon_config.log_driver.is_none() {
        return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
    }

    if let Some(log_opts) = &daemon_config.log_opts {
        if log_opts.max_file.is_none() {
            return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
        }

        if log_opts.max_size.is_none() {
            return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
        }
    } else {
        return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
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
        .with_context(|_| {
            format!(
                "Could not connect to {}:{} : could not resolve hostname",
                iothub_hostname, port,
            )
        })?
        .next()
        .ok_or_else(|| {
            Context::new(format!(
                "Could not connect to {}:{} : could not resolve hostname: no addresses found",
                iothub_hostname, port,
            ))
        })?;

    let stream = TcpStream::connect_timeout(&iothub_host, std::time::Duration::from_secs(10))
        .with_context(|_| format!("Could not connect to {}:{}", iothub_hostname, port))?;

    let tls_connector = native_tls::TlsConnector::new().with_context(|_| {
        format!(
            "Could not connect to {}:{} : could not create TLS connector",
            iothub_hostname, port,
        )
    })?;

    let _ = tls_connector
        .connect(iothub_hostname, stream)
        .with_context(|_| {
            format!(
                "Could not connect to {}:{} : could not complete TLS handshake",
                iothub_hostname, port,
            )
        })?;

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

    if let Err((_, err)) = docker(docker_host_arg, args) {
        return Err(err
            .context(format!(
                "Container on the {} network could not connect to {}:{}",
                if use_container_runtime_network {
                    network_name
                } else {
                    "default"
                },
                iothub_hostname,
                port,
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

    let inspect_result = docker(docker_host_arg, vec!["inspect", "edgeHub"])
        .map_err(|(_, err)| err)
        .and_then(|output| {
            let (inspect_result,): (docker::models::InlineResponse200,) =
                serde_json::from_slice(&output)
                    .context("could not parse result of docker inspect")?;
            Ok(inspect_result)
        })
        .context("Could not check current state of Edge Hub container")?;

    let is_running = inspect_result
        .state()
        .and_then(docker::models::InlineResponse200State::running)
        .cloned()
        .ok_or_else(|| {
            Context::new(
                "Could not check current state of Edge Hub container: \
                 could not parse result of docker inspect: state.status is not set",
            )
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
                "Could not check port bindings of Edge Hub container: \
                 could not parse result of docker inspect: host_config.port_bindings is not set",
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
                    "Edge hub cannot start on device because port {} is already in use.\n\
                     Please stop the application using the port or remove the port binding from Edge hub's deployment.",
                    port_binding,
                )).into());
            }

            #[cfg(unix)]
            Err(ref err) if err.kind() == std::io::ErrorKind::PermissionDenied => {
                return Ok(CheckResult::Fatal(Context::new(format!(
                    "Permission denied when attempting to bind to port {}. You might need to run this command as root.",
                    port_binding,
                )).into()));
            }

            Err(err) => {
                return Err(err
                    .context(format!(
                        "Could not check if port {} is available for Edge Hub to bind to",
                        port_binding,
                    ))
                    .into());
            }
        }
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

fn docker<I>(docker_host_arg: &str, args: I) -> Result<Vec<u8>, (Option<String>, failure::Error)>
where
    I: IntoIterator,
    <I as IntoIterator>::Item: AsRef<OsStr>,
{
    let mut process = Command::new("docker");
    process.arg("-H");
    process.arg(docker_host_arg);

    process.args(args);

    let output = process.output().map_err(|err| {
        (
            None,
            err.context(format!("could not run {:?}", process)).into(),
        )
    })?;

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&*output.stderr).into_owned();
        let err = Context::new(format!(
            "docker returned {}, stderr = {}",
            output.status, stderr,
        ))
        .into();
        return Err((Some(stderr), err));
    }

    Ok(output.stdout)
}

fn write_lines<'a>(
    writer: &mut impl Write,
    first_line_indent: &str,
    other_lines_indent: &str,
    mut lines: impl Iterator<Item = &'a str>,
) -> std::io::Result<()> {
    if let Some(line) = lines.next() {
        writeln!(writer, "{}{}", first_line_indent, line)?;
    }

    for line in lines {
        writeln!(writer, "{}{}", other_lines_indent, line)?;
    }

    Ok(())
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
                filename,
            );

            let mut check = runtime
                .block_on(super::Check::new(
                    config_file.into(),
                    "daemon.json".into(), // unused for this test
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
                Err(err) => {
                    let message = err.to_string();
                    assert!(
                        message
                            .starts_with("config.yaml has hostname localhost but device reports"),
                        "checking hostname in {} produced unexpected error: {}",
                        filename,
                        message,
                    );
                }
                check_result => panic!(
                    "checking hostname in {} returned {:?}",
                    filename, check_result
                ),
            }

            // Pretend it's Moby
            check.docker_server_version = Some("3.0.3".to_owned());

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
            filename,
        );

        let mut check = runtime
            .block_on(super::Check::new(
                config_file.into(),
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Some("1.0.0".to_owned()),      // unused for this test
                "iotedged".into(),             // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                false,
            ))
            .unwrap();

        match super::parse_settings(&mut check) {
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

            check_result => panic!("parsing {} returned {:?}", filename, check_result),
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
            filename,
        );

        let mut check = runtime
            .block_on(super::Check::new(
                config_file.into(),
                "daemon.json".into(), // unused for this test
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

        // Pretend it's Moby even though named pipe indicates otherwise
        check.docker_server_version = Some("3.0.3".to_owned());

        match super::settings_moby_runtime_uri(&mut check) {
            Ok(super::CheckResult::Warning(warning)) => assert!(
                warning.to_string().contains(
                    "Device is not using a production-supported container engine (moby-engine)."
                ),
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
            filename,
        );

        let mut check = runtime
            .block_on(super::Check::new(
                config_file.into(),
                "daemon.json".into(), // unused for this test
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
        check.docker_server_version = Some("18.09.1".to_owned());

        match super::settings_moby_runtime_uri(&mut check) {
            Ok(super::CheckResult::Warning(warning)) => assert!(
                warning.to_string().contains(
                    "Device is not using a production-supported container engine (moby-engine)."
                ),
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
