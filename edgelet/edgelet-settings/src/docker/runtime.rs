// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct MobyRuntime {
    uri: url::Url,
    network: crate::docker::network::MobyNetwork,

    // #[serde(skip_serializing_if = "Option::is_none")]
    // content_trust: Option<ContentTrust>,
}
