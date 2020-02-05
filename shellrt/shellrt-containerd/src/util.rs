use lazy_static::lazy_static;
use tonic::metadata::AsciiMetadataValue;
use tonic::Request;

lazy_static! {
    // HACK: containerd hardcodes cri namespace to "k8s.io," so we have to match it
    static ref NAMESPACE: AsciiMetadataValue = "k8s.io".parse().unwrap();
}

/// Extension trait to easily create new containerd namespaced tonic requests
pub trait TonicRequestExt<T> {
    fn new_namespaced(req: T) -> Request<T>;
}

impl<T> TonicRequestExt<T> for Request<T> {
    /// Create a new gRPC request with metadata
    /// {"containerd-namespace":"<shellrt-containerd-namespace>"}
    fn new_namespaced(msg: T) -> Request<T> {
        let mut req = Request::new(msg);
        req.metadata_mut()
            .insert("containerd-namespace", NAMESPACE.clone());
        req
    }
}

use crate::error::*;
use cri_grpc::{runtimeservice_client::RuntimeServiceClient, ListContainersRequest};

/// Returns the container_id associated with a moddule name. Returns an error if
/// the module doesn't exist.
pub async fn module_to_container_id(
    mut cri_client: RuntimeServiceClient<tonic::transport::Channel>,
    name: &str,
) -> Result<String> {
    // get the corresponding container_id
    let containers = cri_client
        .list_containers(ListContainersRequest { filter: None })
        .await
        .context(ErrorKind::GrpcUnexpectedErr)?
        .into_inner()
        .containers;
    let container = containers.into_iter().find(|c| {
        name == c
            .metadata
            .as_ref()
            .expect("somehow received a null container metadata response")
            .name
    });
    match container {
        Some(container) => Ok(container.id),
        None => Err(ErrorKind::ModuleDoesNotExist.into()),
    }
}
