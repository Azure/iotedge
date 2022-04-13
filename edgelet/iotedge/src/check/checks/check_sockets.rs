use std::os::unix::prelude::PermissionsExt;

use edgelet_core::UrlExt;
use edgelet_settings::RuntimeSettings;
use failure::{Context, ResultExt};
use url::Url;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

const MANAGEMENT_SOCKET_DEFAULT_PERMISSION: u32 = 0o660;
const WORKLOAD_SOCKET_DEFAULT_PERMISSION: u32 = 0o666;

#[derive(Default, serde_derive::Serialize)]
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
    async fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        match &check.settings {
            Some(settings) => {
                let connect_management_uri = settings.connect().management_uri();
                let connect_workload_uri = settings.connect().workload_uri();
                let listen_management_uri = settings.listen().management_uri();
                todo!()
            }
            None => {
                // Check for Existence and Permissions of Both Management and Legacy Workload Connect Sockets
                let connect_management_uri: Url = std::env::var("IOTEDGE_CONNECT_MANAGEMENT_URI")
                    .unwrap_or_else(|_| "unix:///var/run/iotedge/mgmt.sock".to_string())
                    .parse()
                    .expect("failed to parse connnect management uri");

                let connect_management_socket_path = connect_management_uri
                    .to_uds_file_path()
                    .context(
                    "Could not parse connect.management_uri: does not represent a valid file path",
                )?;

                if !connect_management_socket_path.exists() {
                    return Ok(CheckResult::Failed(
                        Context::new(format!("Did not find connect management socket")).into(),
                    ));
                }

                let mgmt_socket_permission = connect_management_socket_path
                    .metadata()?
                    .permissions()
                    .mode();
                if mgmt_socket_permission != MANAGEMENT_SOCKET_DEFAULT_PERMISSION {
                    return Ok(CheckResult::Failed(
                        Context::new(format!(
                            "Incorrect Permission for Connect Management Socket {:o}",
                            mgmt_socket_permission
                        ))
                        .into(),
                    ));
                }

                let legacy_connect_workload_uri: Url =
                    std::env::var("IOTEDGE_CONNECT_WORKLOAD_URI")
                        .unwrap_or_else(|_| "unix:///var/run/iotedge/workload.sock".to_string())
                        .parse()
                        .expect("failed to parse legacy connnect workload uri");

                let workload_management_socket_path = legacy_connect_workload_uri
                    .to_uds_file_path()
                    .context(
                    "Could not parse connect.workload_uri: does not represent a valid file path",
                )?;

                if !workload_management_socket_path.exists() {
                    return Ok(CheckResult::Failed(
                        Context::new(format!("Did not find connect workload socket")).into(),
                    ));
                }

                let workload_socket_permission = workload_management_socket_path
                    .metadata()?
                    .permissions()
                    .mode();
                if workload_socket_permission != WORKLOAD_SOCKET_DEFAULT_PERMISSION {
                    return Ok(CheckResult::Failed(
                        Context::new(format!(
                            "Incorrect Permission for Connect Workload Socket: {:o}",
                            workload_socket_permission
                        ))
                        .into(),
                    ));
                }

                Ok(CheckResult::Ok)
            }
        }
    }
}
