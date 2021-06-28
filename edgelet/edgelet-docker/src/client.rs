// Copyright (c) Microsoft. All rights reserved.

use bollard::Docker;

pub struct DockerClient {
    client: Docker,
}

impl DockerClient {
    pub fn new() -> Result<Self, Box<std::error::Error>> {
        Ok(DockerClient {
            client: Docker::connect_with_local_defaults()?,
        })
    }
}

// impl Deref for DockerClient {
//     type Target = APIClient;

//     fn deref(&self) -> &APIClient {
//         self.client.as_ref()
//     }
// }

impl Clone for DockerClient {
    fn clone(&self) -> Self {
        DockerClient {
            client: self.client.clone(),
        }
    }
}
