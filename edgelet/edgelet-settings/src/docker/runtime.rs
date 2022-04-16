// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct MobyRuntime {
    pub uri: url::Url,
    pub network: crate::docker::network::MobyNetwork,
}

impl MobyRuntime {
    pub fn uri(&self) -> &url::Url {
        &self.uri
    }

    pub fn network(&self) -> &crate::docker::network::MobyNetwork {
        &self.network
    }
}
