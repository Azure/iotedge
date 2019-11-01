use containerd_grpc::containerd::services::images::v1::{client::ImagesClient, DeleteImageRequest};
use shellrt_api::v0::{request, response};

use crate::error::*;
use crate::util::*;

pub struct RemoveHandler {
    grpc_uri: String,
}

impl RemoveHandler {
    pub fn new(grpc_uri: String) -> RemoveHandler {
        RemoveHandler { grpc_uri }
    }

    pub async fn handle(self, req: request::Remove) -> Result<response::Remove> {
        let mut client = ImagesClient::connect(self.grpc_uri)
            .await
            .context(ErrorKind::GrpcConnect)?;

        let grpc_req = tonic::Request::new_namespaced(DeleteImageRequest {
            name: req.image,
            sync: false,
        });
        client
            .delete(grpc_req)
            .await
            .context(ErrorKind::GrpcUnexpectedErr)?;

        Ok(response::Remove {})
    }
}
