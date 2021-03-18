// Copyright (c) Microsoft. All rights reserved.

use std::collections::{BTreeMap, BTreeSet};
use std::io::Write;
use std::path::PathBuf;

use failure::Fail;
use failure::{self, ResultExt};

use edgelet_docker::Settings;

use aziotctl_common::{
    CheckOutputSerializable, CheckOutputSerializableStreaming, CheckResultSerializable,
    CheckResultsSerializable, CheckerMetaSerializable,
};

use crate::error::{Error, ErrorKind};

mod additional_info;
use self::additional_info::AdditionalInfo;

mod stdout;
use self::stdout::Stdout;

mod hostname_checks_common;
mod upstream_protocol_port;

mod checker;
use checker::Checker;

mod checks;

pub struct Check {
    container_engine_config_path: PathBuf,
    diagnostics_image_name: String,
    dont_run: BTreeSet<String>,
    aziot_edged: PathBuf,
    expected_aziot_edged_version: Option<String>,
    output_format: OutputFormat,
    verbose: bool,
    warnings_as_errors: bool,
    aziot_bin: std::ffi::OsString,

    additional_info: AdditionalInfo,

    // These optional fields are populated by the checks
    iothub_hostname: Option<String>, // populated by `aziot check`
    proxy_uri: Option<String>,       // populated by `aziot check`
    settings: Option<Settings>,
    docker_host_arg: Option<String>,
    docker_server_version: Option<String>,
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
        container_engine_config_path: PathBuf,
        diagnostics_image_name: String,
        dont_run: BTreeSet<String>,
        expected_aziot_edged_version: Option<String>,
        aziot_edged: PathBuf,
        output_format: OutputFormat,
        verbose: bool,
        warnings_as_errors: bool,
        aziot_bin: std::ffi::OsString,
        iothub_hostname: Option<String>,
        proxy_uri: Option<String>,
    ) -> Check {
        Check {
            container_engine_config_path,
            diagnostics_image_name,
            dont_run,
            aziot_edged,
            expected_aziot_edged_version,
            output_format,
            verbose,
            warnings_as_errors,
            aziot_bin,

            additional_info: AdditionalInfo::new(),

            iothub_hostname,
            proxy_uri,
            settings: None,
            docker_host_arg: None,
            docker_server_version: None,
        }
    }

    pub fn print_list(aziot_bin: &str) -> Result<(), Error> {
        let mut all_checks: Vec<(String, Vec<CheckerMetaSerializable>)> = Vec::new();

        // get all the aziot checks by shelling-out to aziot
        {
            let aziot_check_out = std::process::Command::new(aziot_bin)
                .arg("check-list")
                .arg("--output=json")
                .output();

            match aziot_check_out {
                Ok(out) => {
                    let aziot_checks: BTreeMap<String, Vec<CheckerMetaSerializable>> =
                        serde_json::from_slice(&out.stdout).context(ErrorKind::Aziot)?;

                    all_checks.extend(aziot_checks.into_iter().map(|(section_name, checks)| {
                        (section_name + " (aziot-identity-service)", checks)
                    }));
                }
                Err(_) => {
                    // not being able to shell-out to aziot is bad... but we shouldn't fail here,
                    // as there might be other iotedge specific checks that don't rely on aziot.
                    //
                    // to make sure the user knows that there should me more checks, we add
                    // this "dummy" entry instead.
                    all_checks.push((
                        "(aziot-identity-service)".into(),
                        vec![CheckerMetaSerializable {
                            id: "(aziot-identity-service-error)".into(),
                            description: format!(
                                "(aziot-identity-service checks unavailable - could not communicate with '{}' binary)",
                                aziot_bin
                            ),
                        }]
                    ));
                }
            }
        }

        // get all the built-in checks
        {
            let built_in_checks = checks::built_in_checks();
            let checks = built_in_checks.iter().map(|(section_name, checks)| {
                (
                    (*section_name).to_string(),
                    checks
                        .iter()
                        .map(|c| CheckerMetaSerializable {
                            id: c.id().into(),
                            description: c.description().into(),
                        })
                        .collect::<Vec<_>>(),
                )
            });

            all_checks.extend(checks);
        }

        // All our text is ASCII, so we can measure text width in bytes rather than using unicode-segmentation to count graphemes.
        let widest_section_name_len = all_checks
            .iter()
            .map(|(section_name, _)| section_name.len())
            .max()
            .expect("Have at least one section");
        let section_name_column_width = widest_section_name_len + 1;
        let widest_check_id_len = all_checks
            .iter()
            .flat_map(|(_, section_checks)| section_checks)
            .map(|check_meta| check_meta.id.len())
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

        for (section_name, section_checks) in all_checks {
            for check_meta in section_checks {
                println!(
                    "{:section_name_column_width$}{:check_id_column_width$}{}",
                    section_name,
                    check_meta.id,
                    check_meta.description,
                    section_name_column_width = section_name_column_width,
                    check_id_column_width = check_id_column_width,
                );
            }

            println!();
        }

        Ok(())
    }

    fn output_section(&self, section_name: &str) {
        if self.output_format == OutputFormat::Text {
            println!();
            println!("{}", section_name);
            println!("{}", "-".repeat(section_name.len()));
        }
    }

    pub fn execute(&mut self, runtime: &mut tokio::runtime::Runtime) -> Result<(), Error> {
        // heterogeneous type representing the output of a check, regardless of
        // whether or not it is built-in, or parsed from `aziot check`
        #[derive(Debug)]
        struct CheckOutput {
            id: String,
            description: String,
            result: CheckResult,
            additional_info: serde_json::Value,
        };

        let mut checks: BTreeMap<String, CheckOutputSerializable> = Default::default();

        let mut stdout = Stdout::new(self.output_format);

        let mut num_successful = 0_usize;
        let mut num_warnings = 0_usize;
        let mut num_skipped = 0_usize;
        let mut num_fatal = 0_usize;
        let mut num_errors = 0_usize;

        let mut output_check = |check: CheckOutput,
                                verbose: bool,
                                warnings_as_errors: bool|
         -> Result<bool, Error> {
            if num_fatal > 0 {
                return Ok(true);
            }

            let CheckOutput {
                id: check_id,
                description: check_name,
                result: check_result,
                additional_info,
                ..
            } = check;

            match check_result {
                CheckResult::Ok => {
                    num_successful += 1;

                    checks.insert(
                        check_id,
                        CheckOutputSerializable {
                            result: CheckResultSerializable::Ok,
                            additional_info,
                        },
                    );

                    stdout.write_success(|stdout| {
                        writeln!(stdout, "\u{221a} {} - OK", check_name)?;
                        Ok(())
                    });
                }

                CheckResult::Warning(ref warning) if !warnings_as_errors => {
                    num_warnings += 1;

                    checks.insert(
                        check_id,
                        CheckOutputSerializable {
                            result: CheckResultSerializable::Warning {
                                details: warning.iter_chain().map(ToString::to_string).collect(),
                            },
                            additional_info,
                        },
                    );

                    stdout.write_warning(|stdout| {
                        writeln!(stdout, "\u{203c} {} - Warning", check_name)?;

                        let message = warning.to_string();

                        write_lines(stdout, "    ", "    ", message.lines())?;

                        if verbose {
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
                            additional_info,
                        },
                    );
                }

                CheckResult::Skipped => {
                    num_skipped += 1;

                    checks.insert(
                        check_id,
                        CheckOutputSerializable {
                            result: CheckResultSerializable::Skipped,
                            additional_info,
                        },
                    );

                    if verbose {
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
                            additional_info,
                        },
                    );

                    stdout.write_error(|stdout| {
                        writeln!(stdout, "\u{00d7} {} - Error", check_name)?;

                        let message = err.to_string();

                        write_lines(stdout, "    ", "    ", message.lines())?;

                        if verbose {
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
                            additional_info,
                        },
                    );

                    stdout.write_error(|stdout| {
                        writeln!(stdout, "\u{00d7} {} - Error", check_name)?;

                        let message = err.to_string();

                        write_lines(stdout, "    ", "    ", message.lines())?;

                        if verbose {
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

            Ok(false)
        };

        // run the aziot checks first, as certain bits of `additional_info` from
        // aziot are required to run iotedge checks. e.g: the "iothub_hostname".
        {
            fn to_check_result(res: CheckResultSerializable) -> CheckResult {
                fn vec_to_err(mut v: Vec<String>) -> failure::Error {
                    let mut err =
                        failure::err_msg(v.pop().expect("errors always have at least one source"));
                    while let Some(s) = v.pop() {
                        err = err.context(s).into();
                    }
                    err
                }

                match res {
                    CheckResultSerializable::Ok => CheckResult::Ok,
                    CheckResultSerializable::Warning { details } => {
                        CheckResult::Warning(vec_to_err(details))
                    }
                    CheckResultSerializable::Ignored => CheckResult::Ignored,
                    CheckResultSerializable::Skipped => CheckResult::Skipped,
                    CheckResultSerializable::Fatal { details } => {
                        CheckResult::Fatal(vec_to_err(details))
                    }
                    CheckResultSerializable::Error { details } => {
                        CheckResult::Failed(vec_to_err(details))
                    }
                }
            }

            let mut aziot_check = std::process::Command::new(&self.aziot_bin);
            aziot_check
                .arg("check")
                .arg("-o=json-stream")
                .stdout(std::process::Stdio::piped());

            if let Some(iothub_hostname) = &self.iothub_hostname {
                aziot_check.arg("--iothub-hostname").arg(iothub_hostname);
            }

            // Prioritize proxy address passed in as command line argument
            // before searching aziot-edged settings for Edge Agent's
            // environment variables.
            if let Some(proxy_uri) = &self.proxy_uri {
                aziot_check.arg("--proxy-uri").arg(proxy_uri.clone());
            } else if let Ok(settings) = Settings::new() {
                if let Some(agent_proxy_uri) = settings.base.agent.env().get("https_proxy") {
                    aziot_check.arg("--proxy-uri").arg(agent_proxy_uri.clone());
                }
            }

            if !self.dont_run.is_empty() {
                aziot_check
                    .arg("--dont-run")
                    .arg(self.dont_run.iter().cloned().collect::<Vec<_>>().join(" "));
            }

            match aziot_check.spawn() {
                Ok(child) => {
                    for val in
                        serde_json::Deserializer::from_reader(child.stdout.unwrap()).into_iter()
                    {
                        let val = val.context(ErrorKind::Aziot)?;
                        match val {
                            CheckOutputSerializableStreaming::Section { name } => {
                                self.output_section(&format!("{} (aziot-identity-service)", name))
                            }
                            CheckOutputSerializableStreaming::Check { meta, output } => {
                                if output_check(
                                    CheckOutput {
                                        id: meta.id,
                                        description: meta.description,
                                        result: to_check_result(output.result),
                                        additional_info: output.additional_info,
                                    },
                                    self.verbose,
                                    self.warnings_as_errors,
                                )? {
                                    break;
                                }
                            }
                            CheckOutputSerializableStreaming::AdditionalInfo(info) => {
                                // if it wasn't manually specified via CLI flag, try to
                                // extract iothub_hostname from additional_info
                                if self.iothub_hostname.is_none() {
                                    self.iothub_hostname = info
                                        .as_object()
                                        .and_then(|m| m.get("iothub_hostname"))
                                        .and_then(serde_json::Value::as_str)
                                        .map(Into::into)
                                }
                            }
                        }
                    }
                }
                Err(err) => {
                    // not being able to shell-out to aziot is bad... but we shouldn't fail here,
                    // as there might be other iotedge specific checks that don't rely on aziot.
                    //
                    // nonetheless, we still need to notify the user that the aziot checks
                    // could not be run.
                    self.output_section("(aziot-identity-service)");
                    output_check(
                        CheckOutput {
                            id: "(aziot-identity-service-error)".into(),
                            description: format!(
                                "aziot-identity-service checks unavailable - could not communicate with '{}' binary.",
                                &self.aziot_bin.to_str().expect("aziot_bin should be valid UTF-8")
                            ),
                            result: CheckResult::Failed(err.context(ErrorKind::Aziot).into()),
                            additional_info: serde_json::Value::Null,
                        },
                        self.verbose,
                        self.warnings_as_errors,
                    )?;
                }
            };
        }

        // run the built-in checks
        'outer: for (section_name, section_checks) in &mut checks::built_in_checks() {
            self.output_section(&section_name);

            for check in section_checks {
                let check_result = if self.dont_run.contains(check.id()) {
                    CheckResult::Ignored
                } else {
                    check.execute(self, runtime)
                };

                if output_check(
                    CheckOutput {
                        id: check.id().into(),
                        description: check.description().into(),
                        result: check_result,
                        additional_info: check.get_json(),
                    },
                    self.verbose,
                    self.warnings_as_errors,
                )? {
                    break 'outer;
                }
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
                additional_info: serde_json::to_value(&self.additional_info).unwrap(),
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

#[cfg(test)]
mod tests {
    use super::{
        checks::{ContainerEngineIsMoby, Hostname, WellFormedConfig},
        Check, CheckResult, Checker,
    };

    lazy_static::lazy_static! {
        static ref ENV_LOCK: std::sync::Mutex<()> = Default::default();
    }

    #[test]
    fn config_file_checks_ok() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        for filename in &["sample_settings.toml", "sample_settings.tg.filepaths.toml"] {
            let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

            std::env::set_var(
                "AZIOT_EDGED_CONFIG",
                format!(
                    "{}/../edgelet-docker/test/{}/{}",
                    env!("CARGO_MANIFEST_DIR"),
                    "linux",
                    filename,
                ),
            );

            let mut check = Check::new(
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Default::default(),
                Some("1.0.0".to_owned()),  // unused for this test
                "aziot-edged".into(),      // unused for this test
                super::OutputFormat::Text, // unused for this test
                false,
                false,
                "".into(), // unused for this test
                None,
                None,
            );

            match WellFormedConfig::default().execute(&mut check, &mut runtime) {
                CheckResult::Ok => (),
                check_result => panic!("parsing {} returned {:?}", filename, check_result),
            }

            match Hostname::default().execute(&mut check, &mut runtime) {
                CheckResult::Failed(err) => {
                    let message = err.to_string();
                    assert!(
                        message
                            .starts_with("configuration has hostname localhost but device reports"),
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

    #[test]
    fn config_file_checks_ok_old_moby() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        for filename in &["sample_settings.toml", "sample_settings.tg.filepaths.toml"] {
            let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

            std::env::set_var(
                "AZIOT_EDGED_CONFIG",
                format!(
                    "{}/../edgelet-docker/test/{}/{}",
                    env!("CARGO_MANIFEST_DIR"),
                    "linux",
                    filename,
                ),
            );

            let mut check = Check::new(
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Default::default(),
                Some("1.0.0".to_owned()),  // unused for this test
                "aziot-edged".into(),      // unused for this test
                super::OutputFormat::Text, // unused for this test
                false,
                false,
                "".into(), // unused for this test
                None,
                None,
            );

            match WellFormedConfig::default().execute(&mut check, &mut runtime) {
                CheckResult::Ok => (),
                check_result => panic!("parsing {} returned {:?}", filename, check_result),
            }

            match Hostname::default().execute(&mut check, &mut runtime) {
                CheckResult::Failed(err) => {
                    let message = err.to_string();
                    assert!(
                        message
                            .starts_with("configuration has hostname localhost but device reports"),
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

            match ContainerEngineIsMoby::default().execute(&mut check, &mut runtime) {
                CheckResult::Ok => (),
                check_result => panic!(
                    "checking moby_runtime.uri in {} returned {:?}",
                    filename, check_result
                ),
            }
        }
    }

    #[test]
    fn parse_settings_err() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        let filename = "bad_sample_settings.toml";

        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var(
            "AZIOT_EDGED_CONFIG",
            format!(
                "{}/../edgelet-docker/test/{}/{}",
                env!("CARGO_MANIFEST_DIR"),
                "linux",
                filename,
            ),
        );

        let mut check = Check::new(
            "daemon.json".into(), // unused for this test
            "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
            Default::default(),
            Some("1.0.0".to_owned()),  // unused for this test
            "aziot-edged".into(),      // unused for this test
            super::OutputFormat::Text, // unused for this test
            false,
            false,
            "".into(), // unused for this test
            None,
            None,
        );

        match WellFormedConfig::default().execute(&mut check, &mut runtime) {
            CheckResult::Failed(_) => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }
    }

    #[test]
    fn moby_runtime_uri_wants_moby_based_on_server_version() {
        let mut runtime = tokio::runtime::Runtime::new().unwrap();

        let filename = "sample_settings.toml";

        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var(
            "AZIOT_EDGED_CONFIG",
            format!(
                "{}/../edgelet-docker/test/{}/{}",
                env!("CARGO_MANIFEST_DIR"),
                "linux",
                filename,
            ),
        );

        let mut check = super::Check::new(
            "daemon.json".into(), // unused for this test
            "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
            Default::default(),
            Some("1.0.0".to_owned()),  // unused for this test
            "aziot-edged".into(),      // unused for this test
            super::OutputFormat::Text, // unused for this test
            false,
            false,
            "".into(), // unused for this test
            None,
            None,
        );

        match WellFormedConfig::default().execute(&mut check, &mut runtime) {
            CheckResult::Ok => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }

        // Pretend it's Docker
        check.docker_server_version = Some("19.03.12".to_owned());

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
