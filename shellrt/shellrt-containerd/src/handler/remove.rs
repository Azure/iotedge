use cri_grpc::{client::ImageServiceClient, ImageSpec, RemoveImageRequest};
use shellrt_api::v0::{request, response};

use crate::error::*;

pub struct RemoveHandler {
    grpc_uri: String,
}

impl RemoveHandler {
    pub fn new(grpc_uri: String) -> RemoveHandler {
        RemoveHandler { grpc_uri }
    }

    pub async fn handle(self, req: request::Remove) -> Result<response::Remove> {
        let mut client = ImageServiceClient::connect(self.grpc_uri)
            .await
            .context(ErrorKind::GrpcConnect)?;

        let grpc_req = RemoveImageRequest {
            image: Some(ImageSpec { image: req.image }),
        };

        client
            .remove_image(grpc_req)
            .await
            .context(ErrorKind::GrpcUnexpectedErr)?;

        Ok(response::Remove {})
    }
}
