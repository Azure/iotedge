use containerd_grpc::containerd::services::version::v1::client::VersionClient;
use shellrt_api::v0::{request, response};

use crate::error::*;

pub struct RuntimeVersionHandler {
    grpc_uri: String,
}

impl RuntimeVersionHandler {
    pub fn new(grpc_uri: String) -> RuntimeVersionHandler {
        RuntimeVersionHandler { grpc_uri }
    }

    pub async fn handle(self, _req: request::RuntimeVersion) -> Result<response::RuntimeVersion> {
        let mut client = VersionClient::connect(self.grpc_uri)
            .await
            .context(ErrorKind::GrpcConnect)?;

        let grpc_req = tonic::Request::new(());
        let grpc_res = client
            .version(grpc_req)
            .await
            .context(ErrorKind::GrpcUnexpectedErr)?
            .into_inner();

        let res = response::RuntimeVersion {
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
