// Copyright (c) Microsoft. All rights reserved.

use bollard::{Docker, API_DEFAULT_VERSION};
use url::Url;

pub struct DockerClient {
    pub docker: Docker,
}

impl DockerClient {
    pub async fn new(docker_uri: &Url) -> Result<Self, Box<dyn std::error::Error>> {
        let docker = Docker::connect_with_local(docker_uri.as_str(), 120, API_DEFAULT_VERSION)?
            .negotiate_version()
            .await?;

        Ok(DockerClient { docker })
    }
}

impl Clone for DockerClient {
    fn clone(&self) -> Self {
        DockerClient {
            docker: self.docker.clone(),
        }
    }
}
