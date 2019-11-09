use shellrt_api::v0::{request, response};

use crate::error::*;

pub struct CreateHandler {
    grpc_uri: String,
}

impl CreateHandler {
    pub fn new(grpc_uri: String) -> CreateHandler {
        CreateHandler { grpc_uri }
    }

    pub async fn handle(self, req: request::Create) -> Result<response::Create> {
        let _ = (req, self.grpc_uri);

        Ok(response::Create {})
    }
}
