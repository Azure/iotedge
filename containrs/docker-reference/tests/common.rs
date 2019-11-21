use docker_reference::*;
use oci_digest::Digest;

/// Workaround for not being able to create `RawReference`s directly.
#[derive(Debug, Clone)]
pub struct ExpectedRawReference {
    pub path: String,
    pub domain: Option<String>,
    pub tag: Option<String>,
    pub digest: Option<Digest>,
}

impl PartialEq<ExpectedRawReference> for RawReference {
    fn eq(&self, other: &ExpectedRawReference) -> bool {
        RawReference::new(
            other.path.clone(),
            other.domain.clone(),
            other.tag.clone(),
            other.digest.clone(),
        )
        .unwrap()
            == *self
    }
}
