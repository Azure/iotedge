// Copyright (c) Microsoft. All rights reserved.

use std;
use std::collections::{BTreeMap, BTreeSet};
use std::fmt::Debug;
use std::io::Write;
use std::path::PathBuf;

use failure::Fail;
use failure::{self, ResultExt};
use futures::future::{self, FutureResult};
use futures::{Future, IntoFuture, Stream};
#[cfg(unix)]
use libc;
use serde_json;

use edgelet_docker::Settings;
use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;

use crate::error::{Error, ErrorKind, FetchLatestVersionsReason};
use crate::LatestVersions;

mod additional_info;
use self::additional_info::AdditionalInfo;

mod stdout;
use self::stdout::Stdout;

mod upstream_protocol_port;

mod checker;
use checker::Checker;

mod checks;
use checks::*;

pub struct Check {
    config_file: PathBuf,
    container_engine_config_path: PathBuf,
    diagnostics_image_name: String,
    dont_run: BTreeSet<String>,
    iotedged: PathBuf,
    latest_versions: Result<super::LatestVersions, Option<Error>>,
    ntp_server: String,
    output_format: OutputFormat,
    verbose: bool,
    warnings_as_errors: bool,

    additional_info: AdditionalInfo,

    iothub_hostname: Option<String>,

    // These optional fields are populated by the checks
    settings: Option<Settings>,
    docker_host_arg: Option<String>,
    docker_server_version: Option<String>,
    device_ca_cert_path: Option<PathBuf>,
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
pub enum CheckResult {
    /// Check succeeded.
    Ok,

    /// Check failed with a warning.
    Warning(failure::Error),

    /// Check is not applicable and was ignored. Should be treated as success.
    Ignored,

    /// Check was skipped because of errors from some previous checks. Should be treated as an error.
    Skipped,

    /// Check failed, and further checks should be performed.
    Failed(failure::Error),

    /// Check failed, and further checks should not be performed.
    Fatal(failure::Error),
}

impl Check {
    pub fn new(
        config_file: PathBuf,
        container_engine_config_path: PathBuf,
        diagnostics_image_name: String,
        dont_run: BTreeSet<String>,
        expected_iotedged_version: Option<String>,
        iotedged: PathBuf,
        iothub_hostname: Option<String>,
        ntp_server: String,
        output_format: OutputFormat,
        verbose: bool,
        warnings_as_errors: bool,
    ) -> impl Future<Item = Self, Error = Error> + Send {
        let latest_versions = if let Some(expected_iotedged_version) = expected_iotedged_version {
            future::Either::A(future::ok::<_, Error>(LatestVersions {
                iotedged: expected_iotedged_version,
            }))
        } else {
            let proxy = std::env::var("HTTPS_PROXY")
                .ok()
                .or_else(|| std::env::var("https_proxy").ok())
                .map(|proxy| proxy.parse::<hyper::Uri>())
                .transpose()
                .context(ErrorKind::FetchLatestVersions(
                    FetchLatestVersionsReason::CreateClient,
                ));
            let hyper_client = proxy.and_then(|proxy| {
                MaybeProxyClient::new(proxy, None, None).context(ErrorKind::FetchLatestVersions(
                    FetchLatestVersionsReason::CreateClient,
                ))
            });
            let hyper_client = match hyper_client {
                Ok(hyper_client) => hyper_client,
                Err(err) => {
                    return future::Either::A(future::err(err.into()));
                }
            };

            let request = hyper::Request::get("https://aka.ms/latest-iotedge-stable")
                .body(hyper::Body::default())
                .expect("can't fail to create request");

            future::Either::B(
                hyper_client
                    .call(request)
                    .then(|response| -> Result<_, Error> {
                        let response = response.context(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::GetResponse,
                        ))?;
                        Ok(response)
                    })
                    .and_then(move |response| match response.status() {
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

        future::Either::B(latest_versions.then(move |latest_versions| {
            Ok(Check {
                config_file,
                container_engine_config_path,
                diagnostics_image_name,
                dont_run,
                iotedged,
                latest_versions: latest_versions.map_err(Some),
                ntp_server,
                output_format,
                verbose,
                warnings_as_errors,

                additional_info: AdditionalInfo::new(),

                settings: None,
                docker_host_arg: None,
                docker_server_version: None,
                iothub_hostname,
                device_ca_cert_path: None,
            })
        }))
    }

    fn checks() -> [(&'static str, Vec<Box<dyn Checker>>); 2] {
        /* Note: keep ordering consistant. Later tests may depend on earlier tests. */
        [
            (
                "Configuration checks",
                vec![
                    Box::new(WellFormedConfig::default()),
                    Box::new(WellFormedConnectionString::default()),
                    Box::new(ContainerEngineInstalled::default()),
                    Box::new(WindowsHostVersion::default()),
                    Box::new(Hostname::default()),
                    Box::new(IotedgedVersion::default()),
                    Box::new(HostLocalTime::default()),
                    Box::new(ContainerLocalTime::default()),
                    Box::new(ContainerEngineDns::default()),
                    Box::new(ContainerEngineIPv6::default()),
                    Box::new(IdentityCertificateExpiry::default()),
                    Box::new(CertificatesQuickstart::default()),
                    Box::new(ContainerEngineIsMoby::default()),
                    Box::new(ContainerEngineLogrotate::default()),
                    Box::new(EdgeAgentStorageMounted::default()),
                    Box::new(EdgeHubStorageMounted::default()),
                ],
            ),
            ("Connectivity checks", {
                let mut tests: Vec<Box<dyn Checker>> = Vec::new();
                tests.push(Box::new(HostConnectDpsEndpoint::default()));
                tests.extend(get_host_connect_iothub_tests());
                tests.extend(get_host_container_iothub_tests());
                tests
            }),
        ]
    }

    pub fn possible_ids() -> impl Iterator<Item = &'static str> {
        let result: Vec<&'static str> = Check::checks()
            .iter()
            .flat_map(|(_, section_checks)| section_checks)
            .map(|check| check.id())
            .collect();

        result.into_iter()
    }

    pub fn print_list() -> Result<(), Error> {
        // All our text is ASCII, so we can measure text width in bytes rather than using unicode-segmentation to count graphemes.
        let widest_section_name_len = Check::checks()
            .iter()
            .map(|(section_name, _)| section_name.len())
            .max()
            .expect("Have at least one section");
        let section_name_column_width = widest_section_name_len + 1;
        let widest_check_id_len = Check::checks()
            .iter()
            .flat_map(|(_, section_checks)| section_checks)
            .map(|check| check.id().len())
            .max()
            .expect("Have at least one check");
        let check_id_column_width = widest_check_id_len + 1;

        println!(
            "{:section_name_column_width$}{:check_id_column_width$}DESCRIPTION",
            "CATEGORY",
            "ID",
            section_name_column_width = section_name_column_width,
            check_id_column_width = check_id_column_width,
        );
        println!();

        for (section_name, section_checks) in &Check::checks() {
            for check in section_checks {
                println!(
                    "{:section_name_column_width$}{:check_id_column_width$}{}",
                    section_name,
                    check.id(),
                    check.description(),
                    section_name_column_width = section_name_column_width,
                    check_id_column_width = check_id_column_width,
                );
            }

            println!();
        }

        Ok(())
    }

    fn execute_inner(&mut self) -> Result<(), Error> {
        let mut checks: BTreeMap<&str, CheckOutputSerializable> = Default::default();
        let mut check_data = Check::checks();

        let mut stdout = Stdout::new(self.output_format);

        let mut num_successful = 0_usize;
        let mut num_warnings = 0_usize;
        let mut num_skipped = 0_usize;
        let mut num_fatal = 0_usize;
        let mut num_errors = 0_usize;

        for (section_name, section_checks) in &mut check_data {
            if num_fatal > 0 {
                break;
            }

            if self.output_format == OutputFormat::Text {
                println!("{}", section_name);
                println!("{}", "-".repeat(section_name.len()));
            }

            for check in section_checks {
                let check_id = check.id();
                let check_name = check.description();

                if num_fatal > 0 {
                    break;
                }

                let check_result = if self.dont_run.contains(check.id()) {
                    CheckResult::Ignored
                } else {
                    check.result(self)
                };

                match check_result {
                    CheckResult::Ok => {
                        num_successful += 1;

                        checks.insert(
                            check_id,
                            CheckOutputSerializable {
                                result: CheckResultSerializable::Ok,
                                additional_info: check.get_json(),
                            },
                        );

                        stdout.write_success(|stdout| {
                            writeln!(stdout, "\u{221a} {} - OK", check_name)?;
                            Ok(())
                        });
                    }

                    CheckResult::Warning(ref warning) if !self.warnings_as_errors => {
                        num_warnings += 1;

                        checks.insert(
                            check_id,
                            CheckOutputSerializable {
                                result: CheckResultSerializable::Warning {
                                    details: warning
                                        .iter_chain()
                                        .map(ToString::to_string)
                                        .collect(),
                                },
                                additional_info: check.get_json(),
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

                    CheckResult::Ignored => {
                        checks.insert(
                            check_id,
                            CheckOutputSerializable {
                                result: CheckResultSerializable::Ignored,
                                additional_info: check.get_json(),
                            },
                        );
                    }

                    CheckResult::Skipped => {
                        num_skipped += 1;

                        checks.insert(
                            check_id,
                            CheckOutputSerializable {
                                result: CheckResultSerializable::Skipped,
                                additional_info: check.get_json(),
                            },
                        );

                        if self.verbose {
                            stdout.write_warning(|stdout| {
                                writeln!(stdout, "\u{203c} {} - Warning", check_name)?;
                                writeln!(stdout, "    skipping because of previous failures")?;
                                Ok(())
                            });
                        }
                    }

                    CheckResult::Fatal(err) => {
                        num_fatal += 1;

                        checks.insert(
                            check_id,
                            CheckOutputSerializable {
                                result: CheckResultSerializable::Fatal {
                                    details: err.iter_chain().map(ToString::to_string).collect(),
                                },
                                additional_info: check.get_json(),
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

                    CheckResult::Warning(err) | CheckResult::Failed(err) => {
                        num_errors += 1;

                        checks.insert(
                            check_id,
                            CheckOutputSerializable {
                                result: CheckResultSerializable::Error {
                                    details: err.iter_chain().map(ToString::to_string).collect(),
                                },
                                additional_info: check.get_json(),
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

    fn execute(mut self) -> Self::Future {
        self.execute_inner().into_future()
    }
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
    checks: BTreeMap<&'static str, CheckOutputSerializable>,
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

#[derive(Debug, serde_derive::Serialize)]
struct CheckOutputSerializable {
    result: CheckResultSerializable,
    additional_info: serde_json::Value,
}
