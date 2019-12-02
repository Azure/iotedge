use cri_grpc::{client::RuntimeServiceClient, ListContainersRequest};
use shellrt_api::v0::{request, response};

use crate::error::*;

pub struct ListHandler {
    grpc_uri: String,
}

impl ListHandler {
    pub fn new(grpc_uri: String) -> ListHandler {
        ListHandler { grpc_uri }
    }

    pub async fn handle(self, _req: request::List) -> Result<response::List> {
        let mut cri_client = RuntimeServiceClient::connect(self.grpc_uri.clone())
            .await
            .context(ErrorKind::GrpcConnect)?;

        // get the corresponding container_id
        let modules = cri_client
            .list_containers(ListContainersRequest { filter: None })
            .await
            .context(ErrorKind::GrpcUnexpectedErr)?
            .into_inner()
            .containers
            .into_iter()
            .map(|c| {
                c.metadata
                    .expect("somehow received a null container metadata response")
                    .name
            })
            .collect::<Vec<String>>();

        Ok(response::List { modules })
    }
}
