// Copyright (c) Microsoft. All rights reserved.

use bollard::Docker;

pub struct DockerClient {
    pub docker: Docker,
}

impl DockerClient {
    pub fn new() -> Result<Self, Box<dyn std::error::Error>> {
        Ok(DockerClient {
            docker: Docker::connect_with_local_defaults()?,
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
