use cri_grpc::{imageservice_client::ImageServiceClient, ImageSpec, RemoveImageRequest};
use shellrt_api::v0::{request, response};

use crate::error::*;

pub struct ImgRemoveHandler {
    grpc_uri: String,
}

impl ImgRemoveHandler {
    pub fn new(grpc_uri: String) -> ImgRemoveHandler {
        ImgRemoveHandler { grpc_uri }
    }

    pub async fn handle(
        self,
        req: request::ImgRemove,
    ) -> Result<(response::ImgRemove, Option<crate::ResponseThunk>)> {
        let mut client = ImageServiceClient::connect(self.grpc_uri)
            .await
            .context(ErrorKind::GrpcConnect)?;

        // FIXME: add the "docker.io" aliasing logic (like in img_pull)
        let grpc_req = RemoveImageRequest {
            image: Some(ImageSpec { image: req.image }),
        };

        client
            .remove_image(grpc_req)
            .await
            .context(ErrorKind::GrpcUnexpectedErr)?;

        Ok((response::ImgRemove {}, None))
    }
}
