// Copyright (c) Microsoft. All rights reserved.

use url::Uri;
use bollard::Docker;

pub struct DockerClient {
    pub docker: Docker,
}

impl DockerClient {
    pub fn new(_docker_uri: &Uri) -> Result<Self, Box<dyn std::error::Error>> {
        Ok(DockerClient {
            docker: Docker::connect_with_local_defaults()?, // TODO: use docker url
        })
    }
}

// impl Deref for DockerClient {
//     type Target = APIClient;

//     fn deref(&self) -> &APIClient {
//         self.docker.as_ref()
//     }
// }

impl Clone for DockerClient {
    fn clone(&self) -> Self {
        DockerClient {
            docker: self.docker.clone(),
        }
    }
}
