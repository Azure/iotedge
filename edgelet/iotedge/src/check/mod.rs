// Copyright (c) Microsoft. All rights reserved.

use std::collections::{BTreeMap, BTreeSet};
use std::io::Write;
use std::path::PathBuf;

#[cfg(unix)]
use std::process::Command;

use failure::Fail;
use failure::{self, ResultExt};
use futures::{future, Future, Stream};

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
use checks::{
    get_host_connect_iothub_tests, get_host_container_iothub_tests, CertificatesQuickstart,
    ConnectManagementUri, ContainerEngineDns, ContainerEngineIPv6, ContainerEngineInstalled,
    ContainerEngineLogrotate, ContainerLocalTime, EdgeAgentStorageMounted, EdgeHubStorageMounted,
    HostConnectDpsEndpoint, HostLocalTime, Hostname, IdentityCertificateExpiry, IotedgedVersion,
    WellFormedConfig, WellFormedConnectionString, WindowsHostVersion,
};

#[cfg(unix)]
use checks::ProxySettings;

#[cfg(windows)]
use checks::ContainerEngineIsMoby;

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
    aziot_edge_proxy: Option<String>,
    settings: Option<Settings>,
    docker_host_arg: Option<String>,
    docker_proxy: Option<String>,
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

                aziot_edge_proxy: get_local_service_proxy_setting("iotedge"),
                settings: None,
                docker_host_arg: None,
                docker_proxy: get_local_service_proxy_setting("docker"),
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
                    Box::new(ConnectManagementUri::default()),
                    Box::new(IotedgedVersion::default()),
                    Box::new(HostLocalTime::default()),
                    Box::new(ContainerLocalTime::default()),
                    Box::new(ContainerEngineDns::default()),
                    Box::new(ContainerEngineIPv6::default()),
                    Box::new(IdentityCertificateExpiry::default()),
                    Box::new(CertificatesQuickstart::default()),
                    #[cfg(windows)]
                    Box::new(ContainerEngineIsMoby::default()),
                    Box::new(ContainerEngineLogrotate::default()),
                    Box::new(EdgeAgentStorageMounted::default()),
                    Box::new(EdgeHubStorageMounted::default()),
                    #[cfg(unix)]
                    Box::new(ProxySettings::default()),
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
        let checks = Check::checks();
        let widest_section_name_len = checks
            .iter()
            .map(|(section_name, _)| section_name.len())
            .max()
            .expect("Have at least one section");
        let section_name_column_width = widest_section_name_len + 1;
        let widest_check_id_len = checks
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

        for (section_name, section_checks) in &checks {
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

    pub fn execute(&mut self, runtime: &mut tokio::runtime::Runtime) -> Result<(), Error> {
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
                    check.execute(self, runtime)
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

fn get_local_service_proxy_setting(_svc_name: &str) -> Option<String> {
    #[cfg(unix)]
    {
        const PROXY_KEY: &str = "https_proxy";
        let output = Command::new("sh")
            .arg("-c")
            .arg("sudo systemctl show --property=Environment ".to_owned() + _svc_name)
            .output()
            .expect("failed to execute process");
        let stdout = String::from_utf8_lossy(&output.stdout);

        let mut svc_proxy = None;
        let vars = stdout.trim_start_matches("Environment=");
        for var in vars.split(' ') {
            let mut parts = var.split('=');
            if let Some(PROXY_KEY) = parts.next() {
                svc_proxy = parts.next().map(String::from);

                let mut s = match svc_proxy {
                    Some(svc_proxy) => svc_proxy,
                    _ => return svc_proxy,
                };

                // Remove newline
                if s.ends_with('\n') {
                    s.pop();
                }

                return Some(s);
            } // Ignore remaining variables
        }

        svc_proxy
    }

    #[cfg(windows)]
    Option::None
}

#[cfg(test)]
mod tests {
    #[cfg(unix)]
    use std::path::Path;

    #[cfg(unix)]
    use edgelet_docker::Settings;

    use super::{
        Check, CheckResult, Checker, Hostname, WellFormedConfig, WellFormedConnectionString,
    };

    #[cfg(unix)]
    use super::ProxySettings;

    #[cfg(windows)]
    use super::ContainerEngineIsMoby;

    #[cfg(unix)]
    enum MobyProxyState {
        Set,
        NotSet,
    }

    #[cfg(unix)]
    enum EdgeDaemonProxyState {
        Set,
        NotSet,
    }

    #[cfg(unix)]
    enum EdgeAgentProxyState {
        Set,
        NotSet,
    }

    #[cfg(unix)]
    enum ProxySettingsValues {
        Mismatching,
        Matching,
    }

    #[cfg(unix)]
    enum ExpectedCheckResult {
        Success,
        Warning,
    }

    #[cfg(unix)]
    fn proxy_settings_test(
        moby_proxy_state: MobyProxyState,
        edge_daemon_proxy_state: EdgeDaemonProxyState,
        edge_agent_proxy_state: EdgeAgentProxyState,
        proxy_settings_values: ProxySettingsValues,
        expected_check_result: ExpectedCheckResult,
    ) {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        let config_filename = match edge_agent_proxy_state {
            EdgeAgentProxyState::Set => "sample_settings_with_proxy_uri.yaml",
            EdgeAgentProxyState::NotSet => "sample_settings.yaml",
        };

        // Set proxy for IoT Edge Agent in config.yaml
        let config_file = Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("..")
            .join("edgelet-docker")
            .join("test")
            .join("linux")
            .join(config_filename);

        // Create an empty check
        let mut check = runtime
            .block_on(Check::new(
                config_file.clone(),
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Default::default(),
                Some("1.0.0".to_owned()),      // unused for this test
                "iotedged".into(),             // unused for this test
                None,                          // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                super::OutputFormat::Text,     // unused for this test
                false,
                false,
            ))
            .unwrap();

        let settings = match Settings::new(&config_file) {
            Ok(settings) => settings,
            Err(err) => panic!("Unable to create settings object, error {:?}", err),
        };

        check.settings = Some(settings);

        // Set proxy for Moby and for IoT Edge Daemon
        let env_proxy_uri = match proxy_settings_values {
            ProxySettingsValues::Matching => "https://config:123",
            ProxySettingsValues::Mismatching => "https://config:456",
        };

        if let EdgeDaemonProxyState::Set = edge_daemon_proxy_state {
            check.aziot_edge_proxy = Some(env_proxy_uri.to_string());
        };

        if let MobyProxyState::Set = moby_proxy_state {
            check.docker_proxy = Some(env_proxy_uri.to_string());
        };

        match expected_check_result {
            ExpectedCheckResult::Success => {
                match ProxySettings::default().execute(&mut check, &mut runtime) {
                    CheckResult::Ok => (),
                    check_result => panic!("proxy settings check returned {:?}", check_result),
                }
            }
            ExpectedCheckResult::Warning => {
                match ProxySettings::default().execute(&mut check, &mut runtime) {
                    CheckResult::Warning(_) => (),
                    check_result => panic!("proxy settings check returned {:?}", check_result),
                }
            }
        }
    }

    #[test]
    fn config_file_checks_ok() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        for filename in &["sample_settings.yaml", "sample_settings.tg.filepaths.yaml"] {
            let config_file = format!(
                "{}/../edgelet-docker/test/{}/{}",
                env!("CARGO_MANIFEST_DIR"),
                if cfg!(windows) { "windows" } else { "linux" },
                filename,
            );

            let mut check = runtime
                .block_on(Check::new(
                    config_file.into(),
                    "daemon.json".into(), // unused for this test
                    "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                    Default::default(),
                    Some("1.0.0".to_owned()),      // unused for this test
                    "iotedged".into(),             // unused for this test
                    None,                          // unused for this test
                    "pool.ntp.org:123".to_owned(), // unused for this test
                    super::OutputFormat::Text,     // unused for this test
                    false,
                    false,
                ))
                .unwrap();

            match WellFormedConfig::default().execute(&mut check, &mut runtime) {
                CheckResult::Ok => (),
                check_result => panic!("parsing {} returned {:?}", filename, check_result),
            }

            match WellFormedConnectionString::default().execute(&mut check, &mut runtime) {
                CheckResult::Ok => (),
                check_result => panic!(
                    "checking connection string in {} returned {:?}",
                    filename, check_result
                ),
            }

            match Hostname::default().execute(&mut check, &mut runtime) {
                CheckResult::Failed(err) => {
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

            #[cfg(windows)]
            {
                // Pretend it's Moby
                check.docker_server_version = Some("19.03.12+azure".to_owned());

                match ContainerEngineIsMoby::default().execute(&mut check, &mut runtime) {
                    CheckResult::Ok => (),
                    check_result => panic!(
                        "checking moby_runtime.uri in {} returned {:?}",
                        filename, check_result
                    ),
                }
            }
        }
    }

    #[test]
    fn config_file_checks_ok_old_moby() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        for filename in &["sample_settings.yaml", "sample_settings.tg.filepaths.yaml"] {
            let config_file = format!(
                "{}/../edgelet-docker/test/{}/{}",
                env!("CARGO_MANIFEST_DIR"),
                if cfg!(windows) { "windows" } else { "linux" },
                filename,
            );

            let mut check = runtime
                .block_on(Check::new(
                    config_file.into(),
                    "daemon.json".into(), // unused for this test
                    "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                    Default::default(),
                    Some("1.0.0".to_owned()),      // unused for this test
                    "iotedged".into(),             // unused for this test
                    None,                          // unused for this test
                    "pool.ntp.org:123".to_owned(), // unused for this test
                    super::OutputFormat::Text,     // unused for this test
                    false,
                    false,
                ))
                .unwrap();

            match WellFormedConfig::default().execute(&mut check, &mut runtime) {
                CheckResult::Ok => (),
                check_result => panic!("parsing {} returned {:?}", filename, check_result),
            }

            match WellFormedConnectionString::default().execute(&mut check, &mut runtime) {
                CheckResult::Ok => (),
                check_result => panic!(
                    "checking connection string in {} returned {:?}",
                    filename, check_result
                ),
            }

            match Hostname::default().execute(&mut check, &mut runtime) {
                CheckResult::Failed(err) => {
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
        }
    }

    #[test]
    fn parse_settings_err() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        let filename = "bad_sample_settings.yaml";
        let config_file = format!(
            "{}/../edgelet-docker/test/{}/{}",
            env!("CARGO_MANIFEST_DIR"),
            if cfg!(windows) { "windows" } else { "linux" },
            filename,
        );

        let mut check = runtime
            .block_on(Check::new(
                config_file.into(),
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Default::default(),
                Some("1.0.0".to_owned()),      // unused for this test
                "iotedged".into(),             // unused for this test
                None,                          // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                super::OutputFormat::Text,     // unused for this test
                false,
                false,
            ))
            .unwrap();

        match WellFormedConfig::default().execute(&mut check, &mut runtime) {
            CheckResult::Failed(err) => {
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
    fn settings_connection_string_dps_hostname() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        let filename = "sample_settings.dps.sym.yaml";
        let config_file = format!(
            "{}/../edgelet-docker/test/{}/{}",
            env!("CARGO_MANIFEST_DIR"),
            if cfg!(windows) { "windows" } else { "linux" },
            filename,
        );

        let mut check = runtime
            .block_on(Check::new(
                config_file.into(),
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Default::default(),
                Some("1.0.0".to_owned()), // unused for this test
                "iotedged".into(),        // unused for this test
                Some("something.something.com".to_owned()), // pretend user specified --iothub-hostname
                "pool.ntp.org:123".to_owned(),              // unused for this test
                super::OutputFormat::Text,                  // unused for this test
                false,
                false,
            ))
            .unwrap();

        match WellFormedConfig::default().execute(&mut check, &mut runtime) {
            CheckResult::Ok => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }

        match WellFormedConnectionString::default().execute(&mut check, &mut runtime) {
            CheckResult::Ok => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }
    }

    // This test inexplicably fails in the ci pipeline due to file read errors.
    // It has been tested on ubuntu 18.04, raspbian buster and windows.
    // It is disabled until the test pipeline issue is resolved.
    // use std::fs::File;
    // use std::io::{BufRead, BufReader, Write};

    // use tempfile::tempdir;
    // #[test]
    // fn settings_connection_string_dps_config_file() {
    //     let mut runtime = tokio::runtime::Runtime::new().unwrap();
    //     let hub_name = "hub_1";

    //     let filename = "sample_settings.dps.sym.yaml";
    //     let config_file_source = format!(
    //         "{}/../edgelet-docker/test/{}/{}",
    //         env!("CARGO_MANIFEST_DIR"),
    //         if cfg!(windows) { "windows" } else { "linux" },
    //         filename,
    //     );

    //     let tmp_dir = tempdir().unwrap();
    //     let config_file = tmp_dir.path().join(filename);
    //     let provision_file = tmp_dir
    //         .path()
    //         .join("cache")
    //         .join("provisioning_backup.json");
    //     std::fs::create_dir(tmp_dir.path().join("cache")).unwrap();

    //     // replace homedir with temp directory
    //     {
    //         let mut new_config = File::create(&config_file).unwrap();
    //         for line in BufReader::new(File::open(config_file_source).unwrap()).lines() {
    //             if let Ok(line) = line {
    //                 if line.contains("homedir") {
    //                     let new_line = format!(
    //                         r#"homedir: "{}""#,
    //                         tmp_dir.path().to_str().unwrap().replace(r"\", r"\\")
    //                     );
    //                     new_config.write_all(new_line.as_bytes()).unwrap();
    //                 } else {
    //                     new_config.write_all(line.as_bytes()).unwrap();
    //                 }
    //                 new_config.write_all(b"\n").unwrap();
    //             }
    //         }
    //     }

    //     let fake_result = provisioning::ProvisioningResult::new(
    //         "a",
    //         hub_name,
    //         None,
    //         provisioning::ReprovisioningStatus::default(),
    //         None,
    //     );
    //     provisioning::backup(&fake_result, &provision_file).unwrap();

    //     let mut check = runtime
    //         .block_on(Check::new(
    //             config_file,
    //             "daemon.json".into(), // unused for this test
    //             "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
    //             Default::default(),
    //             Some("1.0.0".to_owned()),      // unused for this test
    //             "iotedged".into(),             // unused for this test
    //             None,                          // pretend user did not specify --iothub-hostname
    //             "pool.ntp.org:123".to_owned(), // unused for this test
    //             super::OutputFormat::Text,     // unused for this test
    //             false,
    //             false,
    //         ))
    //         .unwrap();

    //     match WellFormedConfig::default().execute(&mut check, &mut runtime) {
    //         CheckResult::Ok => (),
    //         check_result => panic!("parsing config {} returned {:?}", filename, check_result),
    //     }

    //     match WellFormedConnectionString::default().execute(&mut check, &mut runtime) {
    //         CheckResult::Ok => {
    //             assert_eq!(check.iothub_hostname, Some(hub_name.to_owned()));
    //         }
    //         check_result => panic!(
    //             "parsing connection string {} returned {:?}",
    //             filename, check_result
    //         ),
    //     }
    // }

    #[test]
    fn settings_connection_string_dps_err() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        let filename = "sample_settings.dps.sym.yaml";
        let config_file = format!(
            "{}/../edgelet-docker/test/{}/{}",
            env!("CARGO_MANIFEST_DIR"),
            if cfg!(windows) { "windows" } else { "linux" },
            filename,
        );

        let mut check = runtime
            .block_on(Check::new(
                config_file.into(),
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Default::default(),
                Some("1.0.0".to_owned()), // unused for this test
                "iotedged".into(),        // unused for this test
                None,
                "pool.ntp.org:123".to_owned(), // unused for this test
                super::OutputFormat::Text,     // unused for this test
                false,
                false,
            ))
            .unwrap();

        match WellFormedConfig::default().execute(&mut check, &mut runtime) {
            CheckResult::Ok => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }

        match WellFormedConnectionString::default().execute(&mut check, &mut runtime) {
            CheckResult::Failed(err) => assert!(err
                .to_string()
                .contains("Could not retrieve iothub_hostname from provisioning file.")),
            check_result => panic!(
                "checking connection string in {} returned {:?}",
                filename, check_result
            ),
        }
    }

    #[test]
    #[cfg(windows)]
    fn moby_runtime_uri_windows_wants_moby_based_on_runtime_uri() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        let filename = "sample_settings_notmoby.yaml";
        let config_file = format!(
            "{}/../edgelet-docker/test/{}/{}",
            env!("CARGO_MANIFEST_DIR"),
            if cfg!(windows) { "windows" } else { "linux" },
            filename,
        );

        let mut check = runtime
            .block_on(Check::new(
                config_file.into(),
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Default::default(),
                Some("1.0.0".to_owned()),      // unused for this test
                "iotedged".into(),             // unused for this test
                None,                          // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                super::OutputFormat::Text,     // unused for this test
                false,
                false,
            ))
            .unwrap();

        match WellFormedConfig::default().execute(&mut check, &mut runtime) {
            CheckResult::Ok => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }

        #[cfg(windows)]
        {
            // Pretend it's Moby even though named pipe indicates otherwise
            check.docker_server_version = Some("19.03.12+azure".to_owned());

            match ContainerEngineIsMoby::default().execute(&mut check, &mut runtime) {
                CheckResult::Warning(warning) => assert!(
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

    #[test]
    fn moby_runtime_uri_wants_moby_based_on_server_version() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        let filename = "sample_settings.yaml";
        let config_file = format!(
            "{}/../edgelet-docker/test/{}/{}",
            env!("CARGO_MANIFEST_DIR"),
            if cfg!(windows) { "windows" } else { "linux" },
            filename,
        );

        let mut check = runtime
            .block_on(super::Check::new(
                config_file.into(),
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Default::default(),
                Some("1.0.0".to_owned()),      // unused for this test
                "iotedged".into(),             // unused for this test
                None,                          // unused for this test
                "pool.ntp.org:123".to_owned(), // unused for this test
                super::OutputFormat::Text,     // unused for this test
                false,
                false,
            ))
            .unwrap();

        match WellFormedConfig::default().execute(&mut check, &mut runtime) {
            CheckResult::Ok => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }
    }

    #[test]
    #[cfg(unix)]
    fn proxy_settings_iot_edge_agent_not_set_should_fail_test() {
        // Proxy needs to be set in 3 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [x] Moby Daemon
        // [ ] IoT Edge Agent
        // [x] IoT Edge Daemon

        proxy_settings_test(
            MobyProxyState::Set,
            EdgeDaemonProxyState::Set,
            EdgeAgentProxyState::NotSet,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Warning,
        );
    }

    #[test]
    #[cfg(unix)]
    fn proxy_settings_iot_edge_deamon_not_set_should_fail_test() {
        // Proxy needs to be set in 3 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [x] Moby Daemon
        // [x] IoT Edge Agent
        // [ ] IoT Edge Daemon

        proxy_settings_test(
            MobyProxyState::Set,
            EdgeDaemonProxyState::NotSet,
            EdgeAgentProxyState::Set,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Warning,
        );
    }

    #[test]
    #[cfg(unix)]
    fn proxy_settings_moby_deamon_not_set_should_fail_test() {
        // Proxy needs to be set in 3 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [ ] Moby Daemon
        // [x] IoT Edge Agent
        // [x] IoT Edge Daemon

        proxy_settings_test(
            MobyProxyState::NotSet,
            EdgeDaemonProxyState::Set,
            EdgeAgentProxyState::Set,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Warning,
        );
    }

    #[test]
    #[cfg(unix)]
    fn proxy_settings_mismatching_values_should_fail_test() {
        // Proxy needs to be set in 3 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [x] Moby Daemon
        // [x] IoT Edge Agent
        // [x] IoT Edge Daemon

        proxy_settings_test(
            MobyProxyState::Set,
            EdgeDaemonProxyState::Set,
            EdgeAgentProxyState::Set,
            ProxySettingsValues::Mismatching,
            ExpectedCheckResult::Warning,
        );
    }

    #[test]
    #[cfg(unix)]
    fn proxy_settings_all_set_should_succeed_test() {
        // Proxy needs to be set in 3 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [x] Moby Daemon
        // [x] IoT Edge Agent
        // [x] IoT Edge Daemon

        proxy_settings_test(
            MobyProxyState::Set,
            EdgeDaemonProxyState::Set,
            EdgeAgentProxyState::Set,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Success,
        );
    }

    #[test]
    #[cfg(unix)]
    fn proxy_settings_none_set_should_succeed_test() {
        // Proxy needs to be set in 3 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [ ] Moby Daemon
        // [ ] IoT Edge Agent
        // [ ] IoT Edge Daemon

        proxy_settings_test(
            MobyProxyState::NotSet,
            EdgeDaemonProxyState::NotSet,
            EdgeAgentProxyState::NotSet,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Success,
        );
    }
}
