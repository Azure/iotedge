use std::os::unix::prelude::{MetadataExt, PermissionsExt};

use edgelet_core::UrlExt;
use edgelet_settings::RuntimeSettings;
use failure::{Context, ResultExt};
use nix::unistd::{Uid, User, Group};

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

const MANAGEMENT_SOCKET_DEFAULT_PERMISSION: u32 = 0o660;
const WORKLOAD_SOCKET_DEFAULT_PERMISSION: u32 = 0o666;
const DEFAULT_SOCKET_USER: &str = "iotedge";

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
        // Todo : We need to add a similar check in IIS repo for IIS Sockets
        let (connect_management_uri, connect_workload_uri) = match edgelet_settings::Settings::new(){
            Ok(settings) => (
                settings.connect().management_uri().clone(),
                settings.connect().workload_uri().clone(),
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
                "Could not parse socket uri {}: does not represent a valid file path",socket_uri
            ))?;

            if !socket_path.exists() {
                return Ok(CheckResult::Failed(
                    Context::new(format!("Did not find socket with uri {}",socket_uri)).into(),
                ));
            }

            let socket_permission = socket_path
            .metadata()?
            .permissions()
            .mode()
            & 0o777;

            if socket_permission != *permission {
                return Ok(CheckResult::Failed(
                    Context::new(format!(
                        "Incorrect Permission for Socker with URI:{}, Expected Permission: {}, Actual Permission: {}",
                        socket_uri, permission, socket_permission
                    ))
                    .into(),
                ));
            }

            let user = User::from_uid(Uid::from_raw(socket_path.metadata()?.uid()))?;
            if let Some(user) = user {
                let group = Group::from_gid(user.gid)?.unwrap();
                if group.name != *DEFAULT_SOCKET_USER {
                    return Ok(CheckResult::Failed(
                        Context::new(format!(
                            "Incorrect Group for Socket {} User : {}",
                            socket_uri, user.name
                        ))
                        .into(),
                    ));
                }
            } else {
                return Ok(CheckResult::Failed(
                    Context::new(format!("No User for Socket {}", socket_uri)).into(),
                ));
            }
        }

        Ok(CheckResult::Ok)
    }
}
