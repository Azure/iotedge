// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct MobyRuntime {
    uri: url::Url,
    network: crate::docker::network::MobyNetwork,

    #[serde(skip_serializing_if = "Option::is_none")]
    content_trust: Option<ContentTrust>,
}

impl MobyRuntime {
    pub fn uri(&self) -> &url::Url {
        &self.uri
    }

    pub fn network(&self) -> &crate::docker::network::MobyNetwork {
        &self.network
    }

    pub fn content_trust(&self) -> Option<&ContentTrust> {
        self.content_trust.as_ref()
    }
}

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct ContentTrust {
    #[serde(default)]
    ca_certs: Option<std::collections::BTreeMap<String, String>>,
}

impl ContentTrust {
    pub fn ca_certs(&self) -> Option<&std::collections::BTreeMap<String, String>> {
        self.ca_certs.as_ref()
    }
}
