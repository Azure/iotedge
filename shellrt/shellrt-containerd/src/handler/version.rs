use containerd_grpc::containerd::services::version::v1::client::VersionClient;
use shellrt_api::v0::{request, response};

use crate::error::*;

pub struct VersionHandler {
    grpc_uri: String,
}

impl VersionHandler {
    pub fn new(grpc_uri: String) -> VersionHandler {
        VersionHandler { grpc_uri }
    }

    pub async fn handle(self, _req: request::Version) -> Result<response::Version> {
        let mut client = VersionClient::connect(self.grpc_uri)
            .await
            .context(ErrorKind::GrpcConnect)?;

        let grpc_res = client
            .version(())
            .await
            .context(ErrorKind::GrpcUnexpectedErr)?
            .into_inner();

        let res = response::Version {
            info: format!(
                "shellrt-containerd {}\ncontainerd {} rev {}",
                env!("CARGO_PKG_VERSION"),
                grpc_res.version,
                grpc_res.revision,
            ),
        };

        Ok(res)
    }
}
