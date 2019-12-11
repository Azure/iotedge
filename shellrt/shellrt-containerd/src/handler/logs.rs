use log::*;

use cri_grpc::{runtimeservice_client::RuntimeServiceClient, ContainerStatusRequest};
use shellrt_api::v0::{request, response};

use crate::error::*;
use crate::util::module_to_container_id;

pub struct LogsHandler {
    grpc_uri: String,
}

impl LogsHandler {
    pub fn new(grpc_uri: String) -> LogsHandler {
        LogsHandler { grpc_uri }
    }

    pub async fn handle(
        self,
        log_options: request::Logs,
    ) -> Result<(response::Logs, Option<crate::ResponseThunk>)> {
        let mut cri_client = RuntimeServiceClient::connect(self.grpc_uri.clone())
            .await
            .context(ErrorKind::GrpcConnect)?;

        let status = cri_client
            .container_status(ContainerStatusRequest {
                container_id: module_to_container_id(cri_client.clone(), &log_options.name).await?,
                verbose: false,
            })
            .await
            .context(ErrorKind::GrpcConnect)?
            .into_inner()
            .status
            .expect("somehow received a null status response");

        debug!("Opening log file: {}", status.log_path);
        let mut logs = tokio::fs::File::open(status.log_path)
            .await
            .context(ErrorKind::OpenLogFile)?;

        let thunk: crate::ResponseThunk = Box::new(move |mut output| {
            Box::pin(async move {
                // TODO: implement the various log options (i.e: tail, since, follow)
                //
                // this is a non-trivial set of operations, and will be tricky to get right...
                // - "tail" and "since" shouldn't be too tricky to implement:
                //   - Seek to the end of the file
                //   - Read backwards until the specified time/number of lines is reached
                //     - There's not built-in way to reverse-read a file, but it's doable
                // - "follow" will be the trickiest to implement...
                //   - The current naiive implementation closes the `logs` stream the moment an
                //     EOF is encountered. What _should_ happen is that once the EOF is
                //     encountered, the implementation waits for a bit, and tries to read the
                //     file again, checking if any new data has appeared. This gets even more
                //     complicated once you factor in log-rotation, which might swap out the
                //     actual file descriptor the reader is backed by...
                //   - Another complication is around how to _end_ the log stream if the
                //     container is killed. Docker's leverages its internal pub/sub mechanism to
                //     terminate the stream, but that's not directly exposed anywhere.
                //     - Option 1: periodically perform a container status request to see if the
                //       container is still alive
                //     - Option 2: go through the process of setting up the containerd
                //       `events.proto` infrastructure, and get notified once a container dies
                //       via gRPC.
                //
                // This is quite a bit of work, and given that I end my internship in less than
                // 2 weeks, it's probably best to leave this basic implementation as-is, and try
                // to get more core functionality up and running.
                tokio::io::copy(&mut logs, &mut output).await.map(drop)
            })
        });

        Ok((response::Logs {}, Some(thunk)))
    }
}
