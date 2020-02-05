use cri_grpc::{runtimeservice_client::RuntimeServiceClient, StartContainerRequest};
use shellrt_api::v0::{request, response};

use crate::error::*;
use crate::util::module_to_container_id;

pub struct StartHandler {
    grpc_uri: String,
}

impl StartHandler {
    pub fn new(grpc_uri: String) -> StartHandler {
        StartHandler { grpc_uri }
    }

    pub async fn handle(
        self,
        req: request::Start,
    ) -> Result<(response::Start, Option<crate::ResponseThunk>)> {
        let request::Start { name } = req;

        let mut cri_client = RuntimeServiceClient::connect(self.grpc_uri.clone())
            .await
            .context(ErrorKind::GrpcConnect)?;

        cri_client
            .start_container(StartContainerRequest {
                container_id: module_to_container_id(cri_client.clone(), &name).await?,
            })
            .await
            .context(ErrorKind::GrpcUnexpectedErr)?;

        Ok((response::Start {}, None))
    }
}
