use std;
use std::ffi::OsStr;
use std::process::Command;

use failure::{self, Context, Fail};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub struct ContainerEngineInstalled {
    docker_host_arg: Option<String>,
    docker_server_version: Option<String>,
}

impl Checker for ContainerEngineInstalled {
    fn id(&self) -> &'static str {
        "container-engine-uri"
    }
    fn description(&self) -> &'static str {
        "container engine is installed and functional"
    }
    fn result(&mut self, check: &mut Check) -> CheckResult {
        self.execute(check).unwrap_or_else(CheckResult::Failed)
    }
}

impl ContainerEngineInstalled {
    fn execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
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
                            error_message +=
                                "\nYou might need to run this command as Administrator.";
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

        self.docker_host_arg = check.docker_host_arg.clone();
        self.docker_server_version = check.docker_server_version.clone();

        Ok(CheckResult::Ok)
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
