use shellrt_api::v0::{request, response};

use crate::error::*;

pub struct RestartHandler {
    grpc_uri: String,
}

impl RestartHandler {
    pub fn new(grpc_uri: String) -> RestartHandler {
        RestartHandler { grpc_uri }
    }

    pub async fn handle(
        self,
        req: request::Restart,
    ) -> Result<(response::Restart, Option<crate::ResponseThunk>)> {
        let request::Restart { name } = req;

        // CRI doesn't have an explicit restart command.

        super::remove::RemoveHandler::new(self.grpc_uri.clone())
            .handle(request::Remove { name: name.clone() })
            .await?;

        super::start::StartHandler::new(self.grpc_uri.clone())
            .handle(request::Start { name: name.clone() })
            .await?;

        Ok((response::Restart {}, None))
    }
}
