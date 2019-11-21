use docker_reference::*;

mod common;
use common::ExpectedRawReference;

#[test]
fn azure() {
    assert_eq!(
        "iotedgeresources.azurecr.io/samplemodule:0.0.2-amd64"
            .parse::<RawReference>()
            .unwrap(),
        ExpectedRawReference {
            path: "samplemodule".to_string(),
            domain: Some("iotedgeresources.azurecr.io".to_string()),
            tag: Some("0.0.2-amd64".to_string()),
            digest: None
        }
    );
}
