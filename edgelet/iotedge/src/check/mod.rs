// Copyright (c) Microsoft. All rights reserved.

use std::collections::{BTreeMap, BTreeSet};
use std::io::Write;
use std::path::PathBuf;
use std::process::Command;

use anyhow::Context;

use edgelet_settings::{RuntimeSettings, Settings};

use aziotctl_common::{
    CheckOutputSerializable, CheckOutputSerializableStreaming, CheckResultSerializable,
    CheckResultsSerializable, CheckerMetaSerializable,
};

use crate::error::Error;

mod additional_info;
use self::additional_info::AdditionalInfo;

mod stdout;
use self::stdout::Stdout;

mod upstream_protocol_port;

mod shared;
use shared::{CheckResult, Checker, CheckerMeta};

mod checks;

pub struct Check {
    container_engine_config_path: PathBuf,
    diagnostics_image_name: String,
    dont_run: BTreeSet<String>,
    aziot_edged: PathBuf,
    expected_aziot_edged_version: Option<String>,
    expected_aziot_version: Option<String>,
    output_format: OutputFormat,
    verbose: bool,
    warnings_as_errors: bool,
    aziot_bin: std::ffi::OsString,

    additional_info: AdditionalInfo,

    // These optional fields are populated by the checks
    aziot_edge_proxy: Option<String>,
    aziot_identity_proxy: Option<String>,
    iothub_hostname: Option<String>, // populated by `aziot check`
    proxy_uri: Option<String>,       // populated by `aziot check`
    parent_hostname: Option<String>, // populated by `aziot check`
    settings: Option<Settings>,
    docker_host_arg: Option<String>,
    docker_proxy: Option<String>,
    docker_server_version: Option<String>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum OutputFormat {
    Json,
    Text,
}

impl Check {
    pub fn new(
        container_engine_config_path: PathBuf,
        diagnostics_image_name: String,
        dont_run: BTreeSet<String>,
        expected_aziot_edged_version: Option<String>,
        expected_aziot_version: Option<String>,
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
            expected_aziot_version,
            output_format,
            verbose,
            warnings_as_errors,
            aziot_bin,

            additional_info: AdditionalInfo::new(),

            aziot_edge_proxy: get_local_service_proxy_setting("aziot-edged.service"),
            aziot_identity_proxy: get_local_service_proxy_setting("aziot-identityd.service"),
            iothub_hostname,
            proxy_uri: get_proxy_uri(proxy_uri),
            parent_hostname: None,
            settings: None,
            docker_host_arg: None,
            docker_proxy: get_local_service_proxy_setting("docker"),
            docker_server_version: None,
        }
    }

    pub async fn print_list(aziot_bin: &str) -> anyhow::Result<()> {
        let mut all_checks: Vec<(String, Vec<CheckerMetaSerializable>)> = Vec::new();

        // get all the aziot checks by shelling-out to aziot
        {
            let aziot_check_out = tokio::process::Command::new(aziot_bin)
                .arg("check-list")
                .arg("--output=json")
                .output()
                .await;

            match aziot_check_out {
                Ok(out) => {
                    let aziot_checks: BTreeMap<String, Vec<CheckerMetaSerializable>> =
                        serde_json::from_slice(&out.stdout).context(Error::Aziot)?;

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
                            id: c.meta().id.into(),
                            description: c.meta().description.into(),
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

    pub async fn execute(&mut self) -> anyhow::Result<()> {
        // heterogeneous type representing the output of a check, regardless of
        // whether or not it is built-in, or parsed from `aziot check`
        #[derive(Debug)]
        struct CheckOutput {
            id: String,
            description: String,
            result: CheckResult,
            additional_info: serde_json::Value,
        }

        let mut checks: BTreeMap<String, CheckOutputSerializable> = Default::default();

        let mut stdout = Stdout::new(self.output_format);

        let mut num_successful = 0_usize;
        let mut num_warnings = 0_usize;
        let mut num_skipped = 0_usize;
        let mut num_fatal = 0_usize;
        let mut num_errors = 0_usize;

        let mut output_check =
            |check: CheckOutput, verbose: bool, warnings_as_errors: bool| -> anyhow::Result<bool> {
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
                                    details: warning.chain().map(ToString::to_string).collect(),
                                },
                                additional_info,
                            },
                        );

                        stdout.write_warning(|stdout| {
                            writeln!(stdout, "\u{203c} {} - Warning", check_name)?;

                            let message = warning.to_string();

                            write_lines(stdout, "    ", "    ", message.lines())?;

                            if verbose {
                                for cause in warning.chain() {
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

                    CheckResult::SkippedDueTo(reason) => {
                        num_skipped += 1;

                        checks.insert(
                            check_id,
                            CheckOutputSerializable {
                                result: CheckResultSerializable::Skipped,
                                additional_info,
                            },
                        );

                        if verbose {
                            stdout.write_success(|stdout| {
                                writeln!(stdout, "\u{221a} {} - OK", check_name)?;
                                writeln!(stdout, "    skipping because of {}", reason)?;
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
                                    details: err.chain().map(ToString::to_string).collect(),
                                },
                                additional_info,
                            },
                        );

                        stdout.write_error(|stdout| {
                            writeln!(stdout, "\u{00d7} {} - Error", check_name)?;

                            let message = err.to_string();

                            write_lines(stdout, "    ", "    ", message.lines())?;

                            if verbose {
                                for cause in err.chain() {
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
                                    details: err.chain().map(ToString::to_string).collect(),
                                },
                                additional_info,
                            },
                        );

                        stdout.write_error(|stdout| {
                            writeln!(stdout, "\u{00d7} {} - Error", check_name)?;

                            let message = err.to_string();

                            write_lines(stdout, "    ", "    ", message.lines())?;

                            if verbose {
                                for cause in err.chain() {
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
                fn vec_to_err(mut v: Vec<String>) -> anyhow::Error {
                    let mut err =
                        anyhow::anyhow!(v.pop().expect("errors always have at least one source"),);
                    while let Some(s) = v.pop() {
                        err = err.context(s);
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

            // If a proxy is configured, pass along the proxy-uri
            if let Some(proxy_uri) = &self.proxy_uri {
                aziot_check.arg("--proxy-uri").arg(proxy_uri.clone());
            }

            if !self.dont_run.is_empty() {
                aziot_check
                    .arg("--dont-run")
                    .arg(self.dont_run.iter().cloned().collect::<Vec<_>>().join(" "));
            }

            if let Some(version) = &self.expected_aziot_version {
                aziot_check.arg("--expected-aziot-version").arg(version);
            }

            match aziot_check.spawn() {
                Ok(child) => {
                    for val in
                        serde_json::Deserializer::from_reader(child.stdout.unwrap()).into_iter()
                    {
                        let val = val.context(Error::Aziot)?;
                        match val {
                            CheckOutputSerializableStreaming::Section { name } => {
                                self.output_section(&format!("{} (aziot-identity-service)", name));
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
                                        .map(Into::into);
                                }

                                self.parent_hostname = info
                                    .as_object()
                                    .and_then(|m| m.get("local_gateway_hostname"))
                                    .and_then(serde_json::Value::as_str)
                                    .map(Into::into);
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
                            result: CheckResult::Failed(anyhow::Error::from(err).context(Error::Aziot)),
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
            self.output_section(section_name);

            for check in section_checks {
                let check_result = if self.dont_run.contains(check.meta().id) {
                    CheckResult::Ignored
                } else {
                    check.execute(self).await
                };

                let check_output = CheckOutput {
                    id: check.meta().id.into(),
                    description: check.meta().description.into(),
                    result: check_result,
                    additional_info: serde_json::to_value(check).unwrap(),
                };

                if output_check(check_output, self.verbose, self.warnings_as_errors)? {
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
            Err(Error::Diagnostics.into())
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
                return Err(Error::Diagnostics.into());
            }

            println!();
        }

        result
    }
}

fn get_proxy_uri(arg: Option<String>) -> Option<String> {
    // If proxy address was passed in as command line argument, we are good
    if arg.is_some() {
        return arg;
    }
    // Proxy_address wasn't passed in on the command line. Pull it from the aziot-edged settings
    // for Edge Agent's environment variables.
    if let Ok(settings) = Settings::new() {
        if let Some(agent_proxy_uri) = settings.base.agent().env().get("https_proxy") {
            return Some(agent_proxy_uri.clone());
        }
    }
    // Otherwise, pull it from the environment
    std::env::var("HTTPS_PROXY")
        .ok()
        .or_else(|| std::env::var("https_proxy").ok())
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

fn get_local_service_proxy_setting(svc_name: &str) -> Option<String> {
    const PROXY_KEY: &str = "https_proxy";
    let output = Command::new("sh")
        .arg("-c")
        .arg("systemctl show --property=Environment ".to_owned() + svc_name)
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

#[cfg(test)]
mod tests {
    use edgelet_settings::docker::Settings;

    use super::{checks::ProxySettings, checks::WellFormedConfig, Check, CheckResult, Checker};

    lazy_static::lazy_static! {
        static ref ENV_LOCK: tokio::sync::Mutex<()> = Default::default();
    }

    enum MobyProxyState {
        Set,
        NotSet,
    }

    enum EdgeDaemonProxyState {
        Set,
        NotSet,
    }

    enum IdentityDaemonProxyState {
        Set,
        NotSet,
    }

    enum EdgeAgentProxyState {
        Set,
        NotSet,
    }

    enum ProxySettingsValues {
        Mismatching,
        Matching,
    }

    enum ExpectedCheckResult {
        Success,
        Warning,
    }

    async fn proxy_settings_test(
        moby_proxy_state: MobyProxyState,
        edge_daemon_proxy_state: EdgeDaemonProxyState,
        identity_daemon_proxy_state: IdentityDaemonProxyState,
        edge_agent_proxy_state: EdgeAgentProxyState,
        proxy_settings_values: ProxySettingsValues,
        expected_check_result: ExpectedCheckResult,
    ) {
        // Grab an env lock since we are going to be mucking with the environment.
        let _env_lock = ENV_LOCK.lock().await;
        let config_toml_filename = match edge_agent_proxy_state {
            EdgeAgentProxyState::Set => "sample_settings_with_proxy_uri.toml",
            EdgeAgentProxyState::NotSet => "sample_settings.toml",
        };

        // Unset var to make sure we have a clean start
        std::env::remove_var("AZIOT_EDGED_CONFIG");
        std::env::remove_var("AZIOT_EDGED_CONFIG_DIR");

        // Set proxy for IoT Edge Agent in config.toml
        std::env::set_var(
            "AZIOT_EDGED_CONFIG",
            format!(
                "{}/../edgelet-settings/test-files/{}",
                env!("CARGO_MANIFEST_DIR"),
                config_toml_filename,
            ),
        );

        std::env::set_var(
            "AZIOT_EDGED_CONFIG_DIR",
            format!(
                "{}/../edgelet-settings/test-files/{}",
                env!("CARGO_MANIFEST_DIR"),
                "config.d",
            ),
        );

        // Create an empty check
        let mut check = super::Check::new(
            "daemon.json".into(), // unused for this test
            "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
            Default::default(),   // unused for this test
            Some("1.0.0".to_owned()), // unused for this test
            Some("1.0.0".to_owned()), // unused for this test
            "aziot-edged".into(), // unused for this test
            super::OutputFormat::Text, // unused for this test
            false,                // unused for this test
            false,                // unused for this test
            "".into(),            // unused for this test
            None,                 // unused for this test
            None,                 // unused for this test
        );

        let settings = match Settings::new() {
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

        if let IdentityDaemonProxyState::Set = identity_daemon_proxy_state {
            check.aziot_identity_proxy = Some(env_proxy_uri.to_string());
        };

        if let MobyProxyState::Set = moby_proxy_state {
            check.docker_proxy = Some(env_proxy_uri.to_string());
        };

        match expected_check_result {
            ExpectedCheckResult::Success => {
                match ProxySettings::default().execute(&mut check).await {
                    CheckResult::Ok => (),
                    check_result => panic!("proxy settings check returned {:?}", check_result),
                }
            }
            ExpectedCheckResult::Warning => {
                match ProxySettings::default().execute(&mut check).await {
                    CheckResult::Warning(_) => (),
                    check_result => panic!("proxy settings check returned {:?}", check_result),
                }
            }
        }

        std::env::remove_var("AZIOT_EDGED_CONFIG");
        std::env::remove_var("AZIOT_EDGED_CONFIG_DIR");
    }

    #[tokio::test]
    async fn config_file_checks_ok() {
        for filename in &["sample_settings.toml", "sample_settings.tg.filepaths.toml"] {
            let _env_lock = ENV_LOCK.lock().await;

            std::env::set_var(
                "AZIOT_EDGED_CONFIG",
                format!(
                    "{}/../edgelet-settings/test-files/{}",
                    env!("CARGO_MANIFEST_DIR"),
                    filename,
                ),
            );

            let mut check = Check::new(
                "daemon.json".into(), // unused for this test
                "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
                Default::default(),
                Some("1.0.0".to_owned()),  // unused for this test
                Some("1.0.0".to_owned()),  // unused for this test
                "aziot-edged".into(),      // unused for this test
                super::OutputFormat::Text, // unused for this test
                false,
                false,
                "".into(), // unused for this test
                None,
                None,
            );

            match WellFormedConfig::default().execute(&mut check).await {
                CheckResult::Ok => (),
                check_result => panic!("parsing {} returned {:?}", filename, check_result),
            }
        }
    }

    #[tokio::test]
    async fn parse_settings_err() {
        let filename = "bad_sample_settings.toml";

        let _env_lock = ENV_LOCK.lock().await;

        std::env::set_var(
            "AZIOT_EDGED_CONFIG",
            format!(
                "{}/../edgelet-settings/test-files/{}",
                env!("CARGO_MANIFEST_DIR"),
                filename,
            ),
        );

        let mut check = Check::new(
            "daemon.json".into(), // unused for this test
            "mcr.microsoft.com/azureiotedge-diagnostics:1.0.0".to_owned(), // unused for this test
            Default::default(),
            Some("1.0.0".to_owned()),  // unused for this test
            Some("1.0.0".to_owned()),  // unused for this test
            "aziot-edged".into(),      // unused for this test
            super::OutputFormat::Text, // unused for this test
            false,
            false,
            "".into(), // unused for this test
            None,
            None,
        );

        match WellFormedConfig::default().execute(&mut check).await {
            CheckResult::Failed(_) => (),
            check_result => panic!("parsing {} returned {:?}", filename, check_result),
        }
    }

    #[tokio::test]
    #[allow(clippy::semicolon_if_nothing_returned)]
    async fn pickup_proxy_uri_from_the_right_place() {
        // grab an env lock since we are going to be mucking with the environment.
        let _env_lock = ENV_LOCK.lock().await;

        // unset var to make sure we have a clean start
        std::env::remove_var("AZIOT_EDGED_CONFIG");

        // Setup the https_proxy environment var
        let env_proxy_uri1 = "https://environment1:123";
        std::env::set_var("https_proxy", env_proxy_uri1);
        let proxy_uri = super::get_proxy_uri(Option::None);
        // Validate that the uri is picked up from the environment.
        assert!(
            proxy_uri.is_some(),
            "Unable to get proxy_uri from the environment"
        );
        assert_eq!(
                    proxy_uri.unwrap(),
                    env_proxy_uri1.to_string(),
                    "proxy _uri fetched from the environment var \"https_proxy\" did not match expected value: '{};",
                    env_proxy_uri1
                );

        // Setup the HTTPS_PROXY environment var
        let env_proxy_uri2 = "https://environment2:123";
        std::env::set_var("HTTPS_PROXY", env_proxy_uri2);
        let proxy_uri = super::get_proxy_uri(Option::None);
        // Validate that the uri is picked up from the environment.
        assert!(
            proxy_uri.is_some(),
            "Unable to get proxy_uri from the environment"
        );
        assert_eq!(
            proxy_uri.unwrap(),
            env_proxy_uri2.to_string(),
            "proxy _uri fetched from the environment var \"HTTPS_PROXY\" did not match expected value: '{};",
            env_proxy_uri2
        );

        // Point to a test config
        std::env::set_var(
            "AZIOT_EDGED_CONFIG",
            format!(
                "{}/../edgelet-settings/test-files/{}",
                env!("CARGO_MANIFEST_DIR"),
                "sample_settings_with_proxy_uri.toml",
            ),
        );

        // Get proxy_uri again
        let config_proxy_uri = "https://config:123";
        let proxy_uri = super::get_proxy_uri(Option::None);
        // Validate that the uri is picked up from the config which overrides the value in the env.
        assert!(
            proxy_uri.is_some(),
            "Unable to get proxy_uri from the config"
        );
        assert_eq!(
            proxy_uri.unwrap(),
            config_proxy_uri.to_string(),
            "proxy_uri fetched from the config did not match expected value: '{}'",
            config_proxy_uri,
        );

        // Get proxy-uri by passing in the uri as the parameter
        let parm_proxy_uri = "https://commandline:123";
        let proxy_uri = super::get_proxy_uri(Some(parm_proxy_uri.to_string()));
        // Validate that uri is picked up from the passed in parameter which overrides the value in the env and config
        assert!(
            proxy_uri.is_some(),
            "Unable to get proxy_uri from the command line paramter"
        );
        assert_eq!(
            proxy_uri.unwrap(),
            parm_proxy_uri.to_string(),
            "proxy_uri fetched from the config did not match expected value: '{}'",
            parm_proxy_uri,
        );

        // clean up the env
        std::env::remove_var("AZIOT_EDGED_CONFIG");
        std::env::remove_var("HTTPS_PROXY");
        std::env::remove_var("https_proxy");
    }

    #[tokio::test]
    async fn proxy_settings_iot_edge_agent_not_set_should_fail_test() {
        // Proxy needs to be set in 4 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [x] Moby Daemon
        // [ ] IoT Edge Agent
        // [x] IoT Edge Daemon
        // [x] IoT Identity Daemon

        proxy_settings_test(
            MobyProxyState::Set,
            EdgeDaemonProxyState::Set,
            IdentityDaemonProxyState::Set,
            EdgeAgentProxyState::NotSet,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Warning,
        )
        .await;
    }

    #[tokio::test]
    async fn proxy_settings_iot_edge_deamon_not_set_should_fail_test() {
        // Proxy needs to be set in 4 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [x] Moby Daemon
        // [x] IoT Edge Agent
        // [ ] IoT Edge Daemon
        // [x] IoT Identity Daemon

        proxy_settings_test(
            MobyProxyState::Set,
            EdgeDaemonProxyState::NotSet,
            IdentityDaemonProxyState::Set,
            EdgeAgentProxyState::Set,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Warning,
        )
        .await;
    }

    #[tokio::test]
    async fn proxy_settings_moby_deamon_not_set_should_fail_test() {
        // Proxy needs to be set in 4 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [ ] Moby Daemon
        // [x] IoT Edge Agent
        // [x] IoT Edge Daemon
        // [x] IoT Identity Daemon

        proxy_settings_test(
            MobyProxyState::NotSet,
            EdgeDaemonProxyState::Set,
            IdentityDaemonProxyState::Set,
            EdgeAgentProxyState::Set,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Warning,
        )
        .await;
    }

    #[tokio::test]
    async fn proxy_settings_identity_deamon_not_set_should_fail_test() {
        // Proxy needs to be set in 4 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [x] Moby Daemon
        // [x] IoT Edge Agent
        // [x] IoT Edge Daemon
        // [ ] IoT Identity Daemon

        proxy_settings_test(
            MobyProxyState::Set,
            EdgeDaemonProxyState::Set,
            IdentityDaemonProxyState::NotSet,
            EdgeAgentProxyState::Set,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Warning,
        )
        .await;
    }

    #[tokio::test]
    async fn proxy_settings_mismatching_values_should_fail_test() {
        // Proxy needs to be set in 4 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [x] Moby Daemon
        // [x] IoT Edge Agent
        // [x] IoT Edge Daemon
        // [x] IoT Identity Daemon

        proxy_settings_test(
            MobyProxyState::Set,
            EdgeDaemonProxyState::Set,
            IdentityDaemonProxyState::Set,
            EdgeAgentProxyState::Set,
            ProxySettingsValues::Mismatching,
            ExpectedCheckResult::Warning,
        )
        .await;
    }

    #[tokio::test]
    async fn proxy_settings_all_set_should_succeed_test() {
        // Proxy needs to be set in 4 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [x] Moby Daemon
        // [x] IoT Edge Agent
        // [x] IoT Edge Daemon
        // [x] IoT Identity Daemon

        proxy_settings_test(
            MobyProxyState::Set,
            EdgeDaemonProxyState::Set,
            IdentityDaemonProxyState::Set,
            EdgeAgentProxyState::Set,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Success,
        )
        .await;
    }

    #[tokio::test]
    async fn proxy_settings_none_set_should_succeed_test() {
        // Proxy needs to be set in 4 places, otherwise proxy_settings check will fail
        // This test covers the following configuration
        // [ ] Moby Daemon
        // [ ] IoT Edge Agent
        // [ ] IoT Edge Daemon
        // [ ] IoT Identity Daemon

        proxy_settings_test(
            MobyProxyState::NotSet,
            EdgeDaemonProxyState::NotSet,
            IdentityDaemonProxyState::NotSet,
            EdgeAgentProxyState::NotSet,
            ProxySettingsValues::Matching,
            ExpectedCheckResult::Success,
        )
        .await;
    }
}
