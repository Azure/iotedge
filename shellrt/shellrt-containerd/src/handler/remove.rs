use cri_grpc::{client::RuntimeServiceClient, RemoveContainerRequest};
use shellrt_api::v0::{request, response};

use crate::error::*;
use crate::util::module_to_container_id;

pub struct RemoveHandler {
    grpc_uri: String,
}

impl RemoveHandler {
    pub fn new(grpc_uri: String) -> RemoveHandler {
        RemoveHandler { grpc_uri }
    }

    pub async fn handle(self, req: request::Remove) -> Result<response::Remove> {
        let request::Remove { name } = req;

        // TODO?: Should remove also remove the pod?

        let mut cri_client = RuntimeServiceClient::connect(self.grpc_uri.clone())
            .await
            .context(ErrorKind::GrpcConnect)?;

        cri_client
            .remove_container(RemoveContainerRequest {
                container_id: module_to_container_id(cri_client.clone(), &name).await?,
            })
            .await
            .context(ErrorKind::GrpcUnexpectedErr)?;

        Ok(response::Remove {})
    }
}
