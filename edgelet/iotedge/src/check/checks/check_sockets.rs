use edgelet_core::UrlExt;
use edgelet_settings::RuntimeSettings;
use failure::{Context, ResultExt};
use url::Url;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

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
                // Only check for Management Socket because if settings have not been initialized, no workload socket exists
                let connect_management_uri: Url = std::env::var("IOTEDGE_CONNECT_MANAGEMENT_URI")
                    .unwrap_or_else(|_| "unix:///var/run/iotedge/mgmt.sock".to_string())
                    .parse()
                    .expect("failed to parse connnect management uri");
                let listen_management_uri: Url = std::env::var("IOTEDGE_LISTEN_MANAGEMENT_URI")
                    .unwrap_or_else(|_| "fd://aziot-edged.mgmt.socket".to_string())
                    .parse()
                    .expect("failed to parse listen management uri");

                let connect_management_socket_path = connect_management_uri
                    .to_uds_file_path()
                    .context(
                    "Could not parse connect.management_uri: does not represent a valid file path",
                )?;

                if connect_management_socket_path.exists() {
                    return Ok(CheckResult::Ok);
                } else {
                    return Ok(CheckResult::Failed(
                        Context::new(format!("Did not find connect management socket")).into(),
                    ));
                }
                // let connect_workload_socker_path = connect_workload_uri.to_uds_file_path().context("Could not parse connect.workload_url: does not represent a valid file path")?;

                // if connect_workload_socker_path.exists(){
                //     return Ok(CheckResult::Ok)
                // }
                // else
                // {
                //     return Ok(CheckResult::Failed(Context::new(format!("Did not find connect workload socket")).into()))
                // }
            }
        }
    }
}
