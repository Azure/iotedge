// Copyright (c) Microsoft. All rights reserved.

use std;
use std::borrow::Cow;
use std::collections::BTreeMap;
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

use edgelet_config::{Provisioning, Settings};
use edgelet_core::{self, UrlExt};
use edgelet_docker::DockerConfig;
use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;

use crate::error::{Error, ErrorKind, FetchLatestVersionsReason};
use crate::LatestVersions;

mod additional_info;
use self::additional_info::AdditionalInfo;

mod stdout;
use self::stdout::Stdout;

pub struct Check {
    config_file: PathBuf,
    container_engine_config_path: PathBuf,
    diagnostics_image_name: String,
    iotedged: PathBuf,
    latest_versions: Result<super::LatestVersions, Option<Error>>,
    ntp_server: String,
    output_format: OutputFormat,
    verbose: bool,

    additional_info: AdditionalInfo,

    // These optional fields are populated by the checks
    settings: Option<Settings<DockerConfig>>,
    docker_host_arg: Option<String>,
    docker_server_version: Option<String>,
    iothub_hostname: Option<String>,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum OutputFormat {
    Json,
    Text,
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
        iothub_hostname: Option<String>,
        ntp_server: String,
        output_format: OutputFormat,
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
                Ok(MaybeProxyClient::new(proxy, None, None).context(
                    ErrorKind::FetchLatestVersions(FetchLatestVersionsReason::CreateClient),
                )?)
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
                latest_versions: latest_versions.map_err(Some),
                ntp_server,
                output_format,
                verbose,

                additional_info: AdditionalInfo::new(),

                settings: None,
                docker_host_arg: None,
                docker_server_version: None,
                iothub_hostname,
            })
        })
    }

    fn execute_inner(&mut self) -> Result<(), Error> {
        const CHECKS: &[(
            &str, // Section name
            &[(
                &str,                                                  // Check ID
                &str,                                                  // Check description
                fn(&mut Check) -> Result<CheckResult, failure::Error>, // Check function
            )],
        )] = &[
            (
                "Configuration checks",
                &[
                    ("config-yaml-well-formed", "config.yaml is well-formed", parse_settings),
                    (
                        "connection-string",
                        "config.yaml has well-formed connection string",
                        settings_connection_string,
                    ),
                    (
                        "container-engine-uri",
                        "container engine is installed and functional",
                        container_engine,
                    ),
                    ("windows-host-version", "Windows host version is supported", host_version),
                    ("hostname", "config.yaml has correct hostname", settings_hostname),
                    (
                        "connect-management-uri",
                        "config.yaml has correct URIs for daemon mgmt endpoint",
                        daemon_mgmt_endpoint_uri,
                    ),
                    ("iotedged-version", "latest security daemon", iotedged_version),
                    ("host-local-time", "host time is close to real time", host_local_time),
                    ("container-local-time", "container time is close to host time", container_local_time),
                    ("container-engine-dns", "DNS server", container_engine_dns),
                    ("certificates-quickstart", "production readiness: certificates", settings_certificates),
                    (
                        "certificates-expiry",
                        "production readiness: certificates expiry",
                        settings_certificates_expiry,
                    ),
                    (
                        "container-engine-is-moby",
                        "production readiness: container engine",
                        settings_moby_runtime_uri,
                    ),
                    (
                        "container-engine-logrotate",
                        "production readiness: logs policy",
                        container_engine_logrotate,
                    ),
                ],
            ),
            (
                "Connectivity checks",
                &[
                    (
                        "host-connect-dps-endpoint",
                        "host can connect to and perform TLS handshake with DPS endpoint",
                        connection_to_dps_endpoint,
                    ),
                    (
                        "host-connect-iothub-amqp",
                        "host can connect to and perform TLS handshake with IoT Hub AMQP port",
                        |check| connection_to_iot_hub_host(check, 5671),
                    ),
                    (
                        "host-connect-iothub-https",
                        "host can connect to and perform TLS handshake with IoT Hub HTTPS / WebSockets port",
                        |check| connection_to_iot_hub_host(check, 443),
                    ),
                    (
                        "host-connect-iothub-mqtt",
                        "host can connect to and perform TLS handshake with IoT Hub MQTT port",
                        |check| connection_to_iot_hub_host(check, 8883),
                    ),
                    (
                        "container-default-connect-iothub-amqp",
                        "container on the default network can connect to IoT Hub AMQP port",
                        |check| {
                            if cfg!(windows) {
                                // The default network is the same as the IoT Edge module network,
                                // so let the module network checks handle it.
                                Ok(CheckResult::Ignored)
                            } else {
                                connection_to_iot_hub_container(check, 5671, false)
                            }
                        },
                    ),
                    (
                        "container-default-connect-iothub-https",
                        "container on the default network can connect to IoT Hub HTTPS / WebSockets port",
                        |check| {
                            if cfg!(windows) {
                                // The default network is the same as the IoT Edge module network,
                                // so let the module network checks handle it.
                                Ok(CheckResult::Ignored)
                            } else {
                                connection_to_iot_hub_container(check, 443, false)
                            }
                        },
                    ),
                    (
                        "container-default-connect-iothub-mqtt",
                        "container on the default network can connect to IoT Hub MQTT port",
                        |check| {
                            if cfg!(windows) {
                                // The default network is the same as the IoT Edge module network,
                                // so let the module network checks handle it.
                                Ok(CheckResult::Ignored)
                            } else {
                                connection_to_iot_hub_container(check, 8883, false)
                            }
                        },
                    ),
                    (
                        "container-module-connect-iothub-amqp",
                        "container on the IoT Edge module network can connect to IoT Hub AMQP port",
                        |check| {
                            connection_to_iot_hub_container(check, 5671, true)
                        },
                    ),
                    (
                        "container-module-connect-iothub-https",
                        "container on the IoT Edge module network can connect to IoT Hub HTTPS / WebSockets port",
                        |check| {
                            connection_to_iot_hub_container(check, 443, true)
                        },
                    ),
                    (
                        "container-module-connect-iothub-mqtt",
                        "container on the IoT Edge module network can connect to IoT Hub MQTT port",
                        |check| {
                            connection_to_iot_hub_container(check, 8883, true)
                        },
                    ),
                    ("edgehub-host-ports", "Edge Hub can bind to ports on host", edge_hub_ports_on_host),
                ],
            ),
        ];

        let mut checks: BTreeMap<&str, _> = Default::default();

        let mut stdout = Stdout::new(self.output_format);

        let mut num_successful = 0_usize;
        let mut num_warnings = 0_usize;
        let mut num_skipped = 0_usize;
        let mut num_fatal = 0_usize;
        let mut num_errors = 0_usize;

        for (section_name, section_checks) in CHECKS {
            if num_fatal > 0 {
                break;
            }

            if self.output_format == OutputFormat::Text {
                println!("{}", section_name);
                println!("{}", "-".repeat(section_name.len()));
            }

            for (check_id, check_name, check) in *section_checks {
                if num_fatal > 0 {
                    break;
                }

                match check(self) {
                    Ok(CheckResult::Ok) => {
                        num_successful += 1;

                        checks.insert(check_id, CheckResultSerializable::Ok);

                        stdout.write_success(|stdout| {
                            writeln!(stdout, "\u{221a} {} - OK", check_name)?;
                            Ok(())
                        });
                    }

                    Ok(CheckResult::Warning(warning)) => {
                        num_warnings += 1;

                        checks.insert(
                            check_id,
                            CheckResultSerializable::Warning {
                                details: warning.iter_chain().map(ToString::to_string).collect(),
                            },
                        );

                        stdout.write_warning(|stdout| {
                            writeln!(stdout, "\u{203c} {} - Warning", check_name)?;

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

                    Ok(CheckResult::Ignored) => {
                        checks.insert(check_id, CheckResultSerializable::Ignored);
                    }

                    Ok(CheckResult::Skipped) => {
                        num_skipped += 1;

                        checks.insert(check_id, CheckResultSerializable::Skipped);

                        if self.verbose {
                            stdout.write_warning(|stdout| {
                                writeln!(stdout, "\u{203c} {} - Warning", check_name)?;
                                writeln!(stdout, "    skipping because of previous failures")?;
                                Ok(())
                            });
                        }
                    }

                    Ok(CheckResult::Fatal(err)) => {
                        num_fatal += 1;

                        checks.insert(
                            check_id,
                            CheckResultSerializable::Fatal {
                                details: err.iter_chain().map(ToString::to_string).collect(),
                            },
                        );

                        stdout.write_error(|stdout| {
                            writeln!(stdout, "\u{00d7} {} - Error", check_name)?;

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
                        num_errors += 1;

                        checks.insert(
                            check_id,
                            CheckResultSerializable::Error {
                                details: err.iter_chain().map(ToString::to_string).collect(),
                            },
                        );

                        stdout.write_error(|stdout| {
                            writeln!(stdout, "\u{00d7} {} - Error", check_name)?;

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

            if self.output_format == OutputFormat::Text {
                println!();
            }
        }

        stdout.write_success(|stdout| {
            writeln!(stdout, "{} check(s) succeeded.", num_successful)?;
            Ok(())
        });

        if num_warnings > 0 {
            stdout.write_warning(|stdout| {
                write!(stdout, "{} check(s) raised warnings.", num_warnings)?;
                if self.verbose {
                    writeln!(stdout)?;
                } else {
                    writeln!(stdout, " Re-run with --verbose for more details.")?;
                }
                Ok(())
            });
        }

        if num_fatal + num_errors > 0 {
            stdout.write_error(|stdout| {
                write!(stdout, "{} check(s) raised errors.", num_fatal + num_errors)?;
                if self.verbose {
                    writeln!(stdout)?;
                } else {
                    writeln!(stdout, " Re-run with --verbose for more details.")?;
                }
                Ok(())
            });
        }

        if num_skipped > 0 {
            stdout.write_warning(|stdout| {
                write!(
                    stdout,
                    "{} check(s) were skipped due to errors from other checks.",
                    num_skipped,
                )?;
                if self.verbose {
                    writeln!(stdout)?;
                } else {
                    writeln!(stdout, " Re-run with --verbose for more details.")?;
                }
                Ok(())
            });
        }

        let result = if num_fatal + num_errors > 0 {
            Err(ErrorKind::Diagnostics.into())
        } else {
            Ok(())
        };

        if self.output_format == OutputFormat::Json {
            let check_results = CheckResultsSerializable {
                additional_info: &self.additional_info,
                checks,
            };

            if let Err(err) = serde_json::to_writer(std::io::stdout(), &check_results) {
                eprintln!("Could not write JSON output: {}", err,);
                return Err(ErrorKind::Diagnostics.into());
            }

            println!();
        }

        result
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
    } else if check.iothub_hostname.is_none() {
        return Err(Context::new("Device is not using manual provisioning, so Azure IoT Hub hostname needs to be specified with --iothub-hostname").into());
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

    check.docker_server_version = Some(String::from_utf8_lossy(&output).trim().to_owned());
    check.additional_info.docker_version = check.docker_server_version.clone();

    Ok(CheckResult::Ok)
}

fn host_version(check: &mut Check) -> Result<CheckResult, failure::Error> {
    #[cfg(unix)]
    {
        let _ = check;
        Ok(CheckResult::Ignored)
    }

    #[cfg(windows)]
    {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        let moby_runtime_uri = settings.moby_runtime().uri().to_string();

        if moby_runtime_uri != "npipe://./pipe/iotedge_moby_engine" {
            // Host OS version restriction only applies when using Windows containers,
            // which in turn only happens when using Moby
            return Ok(CheckResult::Ignored);
        }

        let os_version = self::additional_info::os_version().context("Could not get OS version")?;
        match os_version {
            // When using Windows containers, the host OS version must match the container OS version.
            // Since our containers are built with 10.0.17763 base images, we require the same for the host OS.
            //
            // If this needs to be changed, also update the host OS version check in the Windows install script
            // (scripts/windows/setup/IotEdgeSecurityDaemon.ps1)
            (10, 0, 17763, _) => (),

            (major_version, minor_version, build_number, _) => {
                return Ok(CheckResult::Fatal(Context::new(format!(
                    "The host has an unsupported OS version {}.{}.{}. IoT Edge on Windows only supports OS version 10.0.17763.\n\
                     Please see https://aka.ms/iotedge-platsup for details.",
                    major_version, minor_version, build_number,
                )).into()))
            }
        }

        Ok(CheckResult::Ok)
    }
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

    // Technically the value of config_hostname doesn't matter as long as it resolves to this device.
    // However determining that the value resolves to *this device* is not trivial.
    //
    // We could start a server and verify that we can connect to ourselves via that hostname, but starting a
    // publicly-available server is not something to be done trivially.
    //
    // We could enumerate the network interfaces of the device and verify that the IP that the hostname resolves to
    // belongs to one of them, but this requires non-trivial OS-specific code
    // (`getifaddrs` on Linux, `GetIpAddrTable` on Windows).
    //
    // Instead, we punt on this check and assume that everything's fine if config_hostname is identical to the device hostname,
    // or starts with it.
    if config_hostname != machine_hostname
        && !config_hostname.starts_with(&format!("{}.", machine_hostname))
    {
        return Err(Context::new(format!(
            "config.yaml has hostname {} but device reports hostname {}.\n\
             Hostname in config.yaml must either be identical to the device hostname \
             or be a fully-qualified domain name that has the device hostname as the first component.",
            config_hostname, machine_hostname,
        ))
        .into());
    }

    // Some software like Kubernetes and the IoT Hub SDKs for downstream clients require the device hostname to follow RFC 1035.
    // For example, the IoT Hub C# SDK cannot connect to a hostname that contains an `_`.
    if !is_rfc_1035_valid(config_hostname) {
        return Ok(CheckResult::Warning(Context::new(format!(
            "config.yaml has hostname {} which does not comply with RFC 1035.\n\
             \n\
             - Hostname must be between 1 and 255 octets inclusive.\n\
             - Each label in the hostname (component separated by \".\") must be between 1 and 63 octets inclusive.\n\
             - Each label must start with an ASCII alphabet character (a-z), end with an ASCII alphanumeric character (a-z, 0-9), \
               and must contain only ASCII alphanumeric characters or hyphens (a-z, 0-9, \"-\").\n\
             \n\
             Not complying with RFC 1035 may cause errors during the TLS handshake with modules and downstream devices.",
            config_hostname,
        ))
        .into()));
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

    check.additional_info.iotedged_version = Some(version.to_owned());

    if version != latest_versions.iotedged {
        return Ok(CheckResult::Warning(
            Context::new(format!(
                "Installed IoT Edge daemon has version {} but {} is the latest stable version available.\n\
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
            if let Some(file_name) = path.file_name().and_then(OsStr::to_str) {
                if file_name.starts_with("device_ca_alias") && file_name.ends_with(".cert.pem") {
                    device_ca_cert_path = Some(path);
                    break;
                }
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

fn connection_to_dps_endpoint(check: &mut Check) -> Result<CheckResult, failure::Error> {
    let settings = if let Some(settings) = &check.settings {
        settings
    } else {
        return Ok(CheckResult::Skipped);
    };

    let dps_endpoint = if let Provisioning::Dps(dps) = settings.provisioning() {
        dps.global_endpoint()
    } else {
        return Ok(CheckResult::Ignored);
    };

    let dps_hostname = dps_endpoint.host_str().ok_or_else(|| {
        Context::new("URL specified in provisioning.global_endpoint does not have a host")
    })?;

    resolve_and_tls_handshake(&dps_endpoint, dps_hostname, dps_hostname)?;

    Ok(CheckResult::Ok)
}

fn connection_to_iot_hub_host(check: &mut Check, port: u16) -> Result<CheckResult, failure::Error> {
    let iothub_hostname = if let Some(iothub_hostname) = &check.iothub_hostname {
        iothub_hostname
    } else {
        return Ok(CheckResult::Skipped);
    };

    resolve_and_tls_handshake(
        &(&**iothub_hostname, port),
        iothub_hostname,
        &format!("{}:{}", iothub_hostname, port),
    )?;

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

    let network_name = settings.moby_runtime().network().name();

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

// Resolves the given `ToSocketAddrs`, then connects to the first address via TCP and completes a TLS handshake.
//
// `tls_hostname` is used for SNI validation and certificate hostname validation.
//
// `hostname_display` is used for the error messages.
fn resolve_and_tls_handshake(
    to_socket_addrs: &impl std::net::ToSocketAddrs,
    tls_hostname: &str,
    hostname_display: &str,
) -> Result<(), failure::Error> {
    let host_addr = to_socket_addrs
        .to_socket_addrs()
        .with_context(|_| {
            format!(
                "Could not connect to {} : could not resolve hostname",
                hostname_display,
            )
        })?
        .next()
        .ok_or_else(|| {
            Context::new(format!(
                "Could not connect to {} : could not resolve hostname: no addresses found",
                hostname_display,
            ))
        })?;

    let stream = TcpStream::connect_timeout(&host_addr, std::time::Duration::from_secs(10))
        .with_context(|_| format!("Could not connect to {}", hostname_display))?;

    let tls_connector = native_tls::TlsConnector::new().with_context(|_| {
        format!(
            "Could not connect to {} : could not create TLS connector",
            hostname_display,
        )
    })?;

    let _ = tls_connector
        .connect(tls_hostname, stream)
        .with_context(|_| {
            format!(
                "Could not connect to {} : could not complete TLS handshake",
                hostname_display,
            )
        })?;

    Ok(())
}

fn is_rfc_1035_valid(name: &str) -> bool {
    if name.is_empty() || name.len() > 255 {
        return false;
    }

    let mut labels = name.split('.');

    let all_labels_valid = labels.all(|label| {
        if label.len() > 63 {
            return false;
        }

        let first_char = match label.chars().next() {
            Some(c) => c,
            None => return false,
        };
        if first_char < 'a' || first_char > 'z' {
            return false;
        }

        if label
            .chars()
            .any(|c| (c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '-')
        {
            return false;
        }

        let last_char = label
            .chars()
            .last()
            .expect("label has at least one character");
        if (last_char < 'a' || last_char > 'z') && (last_char < '0' || last_char > '9') {
            return false;
        }

        true
    });
    if !all_labels_valid {
        return false;
    }

    true
}

fn write_lines<'a>(
    writer: &mut (impl Write + ?Sized),
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

#[derive(Debug, serde_derive::Serialize)]
struct CheckResultsSerializable<'a> {
    additional_info: &'a AdditionalInfo,
    checks: BTreeMap<&'static str, CheckResultSerializable>,
}

#[derive(Debug, serde_derive::Serialize)]
#[serde(tag = "result")]
#[serde(rename_all = "snake_case")]
enum CheckResultSerializable {
    Ok,
    Warning { details: Vec<String> },
    Ignored,
    Skipped,
    Fatal { details: Vec<String> },
    Error { details: Vec<String> },
}

#[cfg(test)]
mod tests {
    #[test]
    fn config_file_checks_ok() {
        let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();

        for filename in &["sample_settings.yaml", "sample_settings.tg.yaml"] {
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
                    None,                          // unused for this test
                    "pool.ntp.org:123".to_owned(), // unused for this test
                    super::OutputFormat::Text,     // unused for this test
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
                None,                          // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                super::OutputFormat::Text,     // unused for this test
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
    fn settings_connection_string_dps() {
        let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();

        let filename = "sample_settings.dps.sym.yaml";
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
                Some("1.0.0".to_owned()), // unused for this test
                "iotedged".into(),        // unused for this test
                Some("something.something.com".to_owned()), // pretend user specified --iothub-hostname
                "pool.ntp.org:123".to_owned(),              // unused for this test
                super::OutputFormat::Text,                  // unused for this test
                false,
            ))
            .unwrap();

        match super::parse_settings(&mut check) {
            Ok(super::CheckResult::Ok) => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }

        match super::settings_connection_string(&mut check) {
            Ok(super::CheckResult::Ok) => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }
    }

    #[test]
    fn settings_connection_string_dps_err() {
        let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();

        let filename = "sample_settings.dps.sym.yaml";
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
                None,                          // pretend user did not specify --iothub-hostname
                "pool.ntp.org:123".to_owned(), // unused for this test
                super::OutputFormat::Text,     // unused for this test
                false,
            ))
            .unwrap();

        match super::parse_settings(&mut check) {
            Ok(super::CheckResult::Ok) => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }

        match super::settings_connection_string(&mut check) {
            Err(err) => assert!(err.to_string().contains("Device is not using manual provisioning, so Azure IoT Hub hostname needs to be specified with --iothub-hostname")),
            check_result => panic!(
                "checking connection string in {} returned {:?}",
                filename, check_result
            ),
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
                None,                          // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                super::OutputFormat::Text,     // unused for this test
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
                None,                          // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                super::OutputFormat::Text,     // unused for this test
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

    #[test]
    fn test_is_rfc_1035_valid() {
        let longest_valid_label = "a".repeat(63);
        let longest_valid_name = format!(
            "{label}.{label}.{label}.{label_rest}",
            label = longest_valid_label,
            label_rest = "a".repeat(255 - 63 * 3 - 3)
        );
        assert_eq!(longest_valid_name.len(), 255);

        assert!(super::is_rfc_1035_valid("foobar"));
        assert!(super::is_rfc_1035_valid("foobar.baz"));
        assert!(super::is_rfc_1035_valid(&longest_valid_label));
        assert!(super::is_rfc_1035_valid(&format!(
            "{label}.{label}.{label}",
            label = longest_valid_label
        )));
        assert!(super::is_rfc_1035_valid(&longest_valid_name));
        assert!(super::is_rfc_1035_valid("xn--v9ju72g90p.com"));
        assert!(super::is_rfc_1035_valid("xn--a-kz6a.xn--b-kn6b.xn--c-ibu"));

        assert!(!super::is_rfc_1035_valid(&format!(
            "{}a",
            longest_valid_label
        )));
        assert!(!super::is_rfc_1035_valid(&format!(
            "{}a",
            longest_valid_name
        )));
        assert!(!super::is_rfc_1035_valid("01.org"));
        assert!(!super::is_rfc_1035_valid("\u{4eca}\u{65e5}\u{306f}"));
        assert!(!super::is_rfc_1035_valid("\u{4eca}\u{65e5}\u{306f}.com"));
        assert!(!super::is_rfc_1035_valid("a\u{4eca}.b\u{65e5}.c\u{306f}"));
    }
}
