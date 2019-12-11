use cri_grpc::{runtimeservice_client::RuntimeServiceClient, StopContainerRequest};
use shellrt_api::v0::{request, response};

use crate::error::*;
use crate::util::module_to_container_id;

pub struct StopHandler {
    grpc_uri: String,
}

impl StopHandler {
    pub fn new(grpc_uri: String) -> StopHandler {
        StopHandler { grpc_uri }
    }

    pub async fn handle(
        self,
        req: request::Stop,
    ) -> Result<(response::Stop, Option<crate::ResponseThunk>)> {
        let request::Stop { name, timeout } = req;

        // TODO?: Should stop also remove the pod?

        let mut cri_client = RuntimeServiceClient::connect(self.grpc_uri.clone())
            .await
            .context(ErrorKind::GrpcConnect)?;

        cri_client
            .stop_container(StopContainerRequest {
                container_id: module_to_container_id(cri_client.clone(), &name).await?,
                timeout,
            })
            .await
            .context(ErrorKind::GrpcUnexpectedErr)?;

        Ok((response::Stop {}, None))
    }
}
