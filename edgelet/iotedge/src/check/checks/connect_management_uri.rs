use std::borrow::Cow;
use std::ffi::{OsStr, OsString};

use failure::{self, Context, ResultExt};

use edgelet_core::{self, RuntimeSettings, UrlExt};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ConnectManagementUri {
    connect_management_uri: Option<String>,
    listen_management_uri: Option<String>,
}

impl Checker for ConnectManagementUri {
    fn id(&self) -> &'static str {
        "connect-management-uri"
    }
    fn description(&self) -> &'static str {
        "configuration has correct URIs for daemon mgmt endpoint"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl ConnectManagementUri {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
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

        let diagnostics_image_name = if check
            .diagnostics_image_name
            .starts_with("/azureiotedge-diagnostics:")
        {
            settings.parent_hostname().map_or_else(
                || "mcr.microsoft.com".to_string() + &check.diagnostics_image_name,
                |upstream_hostname| upstream_hostname.to_string() + &check.diagnostics_image_name,
            )
        } else {
            check.diagnostics_image_name.clone()
        };

        let connect_management_uri = settings.connect().management_uri();
        let listen_management_uri = settings.listen().management_uri();

        self.connect_management_uri = Some(format!("{}", connect_management_uri));
        self.listen_management_uri = Some(format!("{}", listen_management_uri));

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

            let socket_path =
                socket_path.to_str()
                .ok_or_else(|| Context::new("Could not parse connect.management_uri: file path is not valid utf-8"))?;

            args.push(Cow::Owned(format!("{}:{}", socket_path, socket_path).into()));
        },

        (scheme1, scheme2) if scheme1 != scheme2 => return Err(Context::new(
            format!(
                "configuration has invalid combination of schemes for connect.management_uri ({:?}) and listen.management_uri ({:?})",
                scheme1, scheme2,
            ))
            .into()),

        (scheme, _) => return Err(Context::new(
            format!("Could not parse connect.management_uri: scheme {} is invalid", scheme),
        ).into()),
    }

        args.extend(vec![
            Cow::Borrowed(OsStr::new(&diagnostics_image_name)),
            Cow::Borrowed(OsStr::new("dotnet")),
            Cow::Borrowed(OsStr::new("IotedgeDiagnosticsDotnet.dll")),
            Cow::Borrowed(OsStr::new("edge-agent")),
            Cow::Borrowed(OsStr::new("--management-uri")),
            Cow::Owned(OsString::from(connect_management_uri.to_string())),
        ]);

        match super::docker(docker_host_arg, args) {
            Ok(_) => Ok(CheckResult::Ok),
            Err((Some(stderr), err)) => Err(err.context(stderr).into()),
            Err((None, err)) => Err(err.context("Could not spawn docker process").into()),
        }
    }
}
