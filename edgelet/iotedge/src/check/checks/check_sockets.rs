use std::os::unix::prelude::{MetadataExt, PermissionsExt};

use anyhow::Context;
use edgelet_core::UrlExt;
use edgelet_settings::RuntimeSettings;
use nix::unistd::{Gid, Group};
use url::Url;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

// These are defined in workload.rs and management.rs as well
const MANAGEMENT_SOCKET_DEFAULT_PERMISSION: u32 = 0o660;
const WORKLOAD_SOCKET_DEFAULT_PERMISSION: u32 = 0o666;
const DEFAULT_SOCKET_GROUP: &str = "iotedge";

#[derive(Default, serde::Serialize)]
pub(crate) struct CheckSockets {}

#[async_trait::async_trait]
impl Checker for CheckSockets {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "check-sockets",
            description:
                "IoT Edge Communication sockets are available and have the required permission",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl CheckSockets {
    #[allow(clippy::unused_self)]
    #[allow(unused_variables)]
    async fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        // Todo : We need to add a similar check in IIS repo for IIS Sockets
        let (connect_management_uri, connect_workload_uri, workload_mnt_uri) =
            match edgelet_settings::Settings::new() {
                Ok(settings) => (
                    settings.connect().management_uri().clone(),
                    settings.connect().workload_uri().clone(),
                    edgelet_settings::uri::Listen::workload_mnt_uri(
                        settings.homedir().to_str().unwrap(),
                    )
                    .parse::<Url>()
                    .expect("failed to parse management uri"),
                ),
                _ => {
                    return Ok(CheckResult::Skipped);
                }
            };

        for (socket_uri, permission) in &[
            (connect_management_uri, MANAGEMENT_SOCKET_DEFAULT_PERMISSION),
            (connect_workload_uri, WORKLOAD_SOCKET_DEFAULT_PERMISSION),
        ] {
            let socket_path = socket_uri.to_uds_file_path().context(format!(
                "Could not parse socket uri {}: does not represent a valid file path",
                socket_uri
            ))?;

            if !socket_path.exists() {
                return Ok(CheckResult::Failed(anyhow::anyhow!(format!(
                    "Did not find socket with uri {}",
                    socket_uri
                ))));
            }

            // Mode returns some additional information. Mask the first 9 bits to get the Permission.
            let socket_permission = socket_path.metadata()?.permissions().mode() & 0o777;

            if socket_permission != *permission {
                return Ok(CheckResult::Failed(
                    anyhow::anyhow!(format!(
                        "Incorrect Permission for Socker with URI:{}, Expected Permission: {}, Actual Permission: {}",
                        socket_uri, permission, socket_permission
                    ))
                ));
            }

            let group = Group::from_gid(Gid::from_raw(socket_path.metadata()?.gid()))?;
            if let Some(group) = group {
                if group.name != *DEFAULT_SOCKET_GROUP {
                    return Ok(CheckResult::Failed(anyhow::anyhow!(format!(
                        "Incorrect Group for Socket {} Group : {}",
                        socket_uri, group.name
                    ))));
                }
            } else {
                return Ok(CheckResult::Failed(anyhow::anyhow!(format!(
                    "No User for Socket {}",
                    socket_uri
                ))));
            }
        }

        let workload_mnt_uri_path = workload_mnt_uri.to_uds_file_path().context(format!(
            "Could not parse socket uri {}: does not represent a valid file path",
            workload_mnt_uri
        ))?;
        // Ensure that different workload sockets created for different modules have the required permissions
        if !workload_mnt_uri_path.exists() {
            return Ok(CheckResult::Failed(anyhow::anyhow!(format!(
                "Path for Module Workload Sockets {} does not exists",
                workload_mnt_uri
            ))));
        }

        let files = std::fs::read_dir(workload_mnt_uri_path)?;
        for socket_file in files {
            let socket_file_path = socket_file?.path();
            let socket_permission = socket_file_path.metadata()?.permissions().mode() & 0o777;

            if socket_permission != WORKLOAD_SOCKET_DEFAULT_PERMISSION {
                return Ok(CheckResult::Failed(
                    anyhow::anyhow!(format!(
                        "Incorrect Permission for Socket at :{:?}, Expected Permission: {}, Actual Permission: {}",
                        socket_file_path, WORKLOAD_SOCKET_DEFAULT_PERMISSION, socket_permission
                    ))
                ));
            }
        }

        Ok(CheckResult::Ok)
    }
}
