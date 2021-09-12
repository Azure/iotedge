// Made using https://transform.tools/json-to-rust-serde
// Currently not correct, only for testing purposes

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Deployment {
    #[serde(rename = "deviceId")]
    pub device_id: String,
    #[serde(rename = "moduleId")]
    pub module_id: String,
    pub etag: String,
    #[serde(rename = "deviceEtag")]
    pub device_etag: String,
    pub status: String,
    #[serde(rename = "statusUpdateTime")]
    pub status_update_time: String,
    #[serde(rename = "connectionState")]
    pub connection_state: String,
    #[serde(rename = "lastActivityTime")]
    pub last_activity_time: String,
    #[serde(rename = "cloudToDeviceMessageCount")]
    pub cloud_to_device_message_count: i64,
    #[serde(rename = "authenticationType")]
    pub authentication_type: String,
    #[serde(rename = "x509Thumbprint")]
    pub x509_thumbprint: X509Thumbprint,
    #[serde(rename = "modelId")]
    pub model_id: String,
    pub version: i64,
    pub properties: Properties,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct X509Thumbprint {
    #[serde(rename = "primaryThumbprint")]
    pub primary_thumbprint: Option<String>,
    #[serde(rename = "secondaryThumbprint")]
    pub secondary_thumbprint: Option<String>,
}

#[derive(Default, Debug, Clone, PartialEq, serde_derive::Serialize, serde_derive::Deserialize)]
pub struct Properties {
    pub desired: ::serde_json::Value,
    pub reported: ::serde_json::Value,
}
