use lazy_static::lazy_static;
use tonic::metadata::AsciiMetadataValue;
use tonic::Request;

lazy_static! {
    // XXX: shellrt-containerd should have it's own namespace!
    //      using default makes it easier to validate beahvior with `ctr`
    static ref NAMESPACE: AsciiMetadataValue = "default".parse().unwrap();
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
