use cri_grpc::{client::RuntimeServiceClient, ContainerState, ContainerStatusRequest};
use shellrt_api::v0::{request, response, ModuleStatus};

use crate::error::*;
use crate::util::module_to_container_id;

pub struct StatusHandler {
    grpc_uri: String,
}

impl StatusHandler {
    pub fn new(grpc_uri: String) -> StatusHandler {
        StatusHandler { grpc_uri }
    }

    pub async fn handle(
        self,
        req: request::Status,
    ) -> Result<(response::Status, Option<crate::ResponseThunk>)> {
        let request::Status { name } = req;

        let mut cri_client = RuntimeServiceClient::connect(self.grpc_uri.clone())
            .await
            .context(ErrorKind::GrpcConnect)?;

        let status = cri_client
            .container_status(ContainerStatusRequest {
                container_id: module_to_container_id(cri_client.clone(), &name).await?,
                // see `cri/pkg/server/container_status.go:127` for breakdown of verbose info
                verbose: false,
            })
            .await
            .context(ErrorKind::GrpcConnect)?
            .into_inner()
            .status
            .expect("somehow received a null status response");

        let module_status = match ContainerState::from_i32(status.state)
            .expect("somehow received a invalid container state")
        {
            ContainerState::ContainerCreated => ModuleStatus::Unknown, // no equivalent state...
            ContainerState::ContainerRunning => ModuleStatus::Running,
            ContainerState::ContainerExited => {
                if status.exit_code == 0 {
                    ModuleStatus::Stopped
                } else {
                    ModuleStatus::Failed
                }
            }
            ContainerState::ContainerUnknown => ModuleStatus::Unknown,
        };

        let (finished_at, exit_code) = match status.finished_at {
            // 0 implies the container is still running
            0 => (None, None),
            _ => (Some(status.finished_at), Some(status.exit_code as i64)),
        };

        Ok((
            response::Status {
                status: module_status,
                status_description: format!("{}: {}", status.reason, status.message),
                image_id: status.image_ref,
                started_at: status.started_at,
                finished_at,
                exit_code,
            },
            None,
        ))
    }
}
