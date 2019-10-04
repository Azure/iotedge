use std;
use std::borrow::Cow;
use std::collections::{BTreeMap, BTreeSet};
use std::ffi::{CStr, OsStr, OsString};
use std::fs::File;
use std::io::{Read, Write};
use std::net::TcpStream;
use std::path::{Path, PathBuf};
use std::process::Command;

use failure::Fail;
use failure::{self, Context, ResultExt};
use futures::future::{self, FutureResult};
use futures::{Future, IntoFuture, Stream};
#[cfg(unix)]
use libc;
use regex::Regex;
use serde_json;

use edgelet_core::{
    self, AttestationMethod, ManualAuthMethod, MobyNetwork, Provisioning, RuntimeSettings, UrlExt,
};
use edgelet_docker::Settings;
use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;

use crate::check::{Check, CheckResult};
use crate::error::{Error, ErrorKind, FetchLatestVersionsReason};
use crate::LatestVersions;

use crate::check::checks::*;

pub(crate) trait Checker {
    fn id(&self) -> &'static str;
    fn description(&self) -> &'static str;
    fn result(&self) -> &CheckResult;
}

/* ================================================================================================ */

// #[derive(Default, serde_derive::Serialize)]
// pub(crate) struct WellFormedConfig {
//     result: CheckResult,
//     settings: Option<Settings>,
// }
// impl Checker for WellFormedConfig {
//     fn id(&self) -> &'static str {
//         "config-yaml-well-formed"
//     }
//     fn description(&self) -> &'static str {
//         "config.yaml is well-formed"
//     }
//     fn result(&self) -> &CheckResult {
//         &self.result
//     }
// }
// impl WellFormedConfig {
//     fn new(check: &Check) -> Self {
//         let mut checker = Self::default();
//         checker.result = checker.execute(check).unwrap_or_else(CheckResult::Failed);
//         checker
//     }

//     fn execute(&mut self, check: &Check) -> Result<CheckResult, failure::Error> {
//         let config_file = &check.config_file;

//         // The config crate just returns a "file not found" error when it can't open the file for any reason,
//         // even if the real error was a permissions issue.
//         //
//         // So we first try to open the file for reading ourselves.
//         if let Err(err) = File::open(config_file) {
//             if err.kind() == std::io::ErrorKind::PermissionDenied {
//                 return Ok(CheckResult::Fatal(
//                     err.context(format!(
//                         "Could not open file {}. You might need to run this command as {}.",
//                         config_file.display(),
//                         if cfg!(windows) {
//                             "Administrator"
//                         } else {
//                             "root"
//                         },
//                     ))
//                     .into(),
//                 ));
//             } else {
//                 return Err(err
//                     .context(format!("Could not open file {}", config_file.display()))
//                     .into());
//             }
//         }

//         let settings = match Settings::new(config_file) {
//             Ok(settings) => settings,
//             Err(err) => {
//                 let message = if check.verbose {
//                     format!(
//                     "The IoT Edge daemon's configuration file {} is not well-formed.\n\
//                      Note: In case of syntax errors, the error may not be exactly at the reported line number and position.",
//                     config_file.display(),
//                 )
//                 } else {
//                     format!(
//                         "The IoT Edge daemon's configuration file {} is not well-formed.",
//                         config_file.display(),
//                     )
//                 };
//                 return Err(err.context(message).into());
//             }
//         };

//         self.settings = Some(settings);

//         Ok(CheckResult::Ok)
//     }
// }

/* ================================================================================================ */

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct WellFormedConnectionString {
    result: CheckResult,
    iothub_hostname: Option<String>,
}
impl Checker for WellFormedConnectionString {
    fn id(&self) -> &'static str {
        "connection-string"
    }
    fn description(&self) -> &'static str {
        "config.yaml has well-formed connection string"
    }
    fn result(&self) -> &CheckResult {
        &self.result
    }
}
impl WellFormedConnectionString {
    fn new(check: &Check, config: &WellFormedConfig) -> Self {
        let mut checker = Self::default();
        checker.result = checker
            .execute(check, config)
            .unwrap_or_else(CheckResult::Failed);
        checker
    }

    fn execute(
        &mut self,
        check: &Check,
        config: &WellFormedConfig,
    ) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &config.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        if let Provisioning::Manual(manual) = settings.provisioning() {
            let hub = match manual.authentication_method() {
                ManualAuthMethod::DeviceConnectionString(cs) => {
                    let (_, _, hub) = cs.parse_device_connection_string().context(
                                    "Invalid connection string format detected.\n\
                                    Please check the value of the provisioning.device_connection_string parameter.",
                    )?;
                    hub
                }
                ManualAuthMethod::X509(x509) => x509.iothub_hostname().to_owned(),
            };

            self.iothub_hostname = Some(hub.to_owned());
        } else {
            self.iothub_hostname = check.iothub_hostname.clone();
            if check.iothub_hostname.is_none() {
                return Err(Context::new("Device is not using manual provisioning, so Azure IoT Hub hostname needs to be specified with --iothub-hostname").into());
            }
        };

        Ok(CheckResult::Ok)
    }
}

/* ================================================================================================ */

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ContainerEngineInstalled {
    result: CheckResult,
    docker_host_arg: Option<String>,
    docker_server_version: Option<String>,
    //TODO: add docker server version to additional info
}
impl Checker for ContainerEngineInstalled {
    fn id(&self) -> &'static str {
        "container-engine-uri"
    }
    fn description(&self) -> &'static str {
        "container engine is installed and functional"
    }
    fn result(&self) -> &CheckResult {
        &self.result
    }
}
impl ContainerEngineInstalled {
    fn new(check: &Check, config: &WellFormedConfig) -> Self {
        let mut checker = Self::default();
        checker.result = checker
            .execute(check, config)
            .unwrap_or_else(CheckResult::Failed);
        checker
    }

    fn execute(
        &mut self,
        check: &Check,
        config: &WellFormedConfig,
    ) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &config.settings {
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

        self.docker_host_arg = Some(docker_host_arg);
        self.docker_server_version = Some(String::from_utf8_lossy(&output).trim().to_owned());

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
