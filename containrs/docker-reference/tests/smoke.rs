use docker_reference::*;

#[test]
fn azure() {
    assert_eq!(
        "iotedgeresources.azurecr.io/samplemodule:0.0.2-amd64"
            .parse::<RawReference>()
            .unwrap(),
        RawReference {
            name: "samplemodule".to_string(),
            domain: Some("iotedgeresources.azurecr.io".to_string()),
            tag: Some("0.0.2-amd64".to_string()),
            digest: None
        }
    );
}
