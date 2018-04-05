use std::ops::Deref;
use std::rc::Rc;

use hyper::client::Connect;

use docker::apis::client::APIClient;

pub struct DockerClient<C: Connect> {
    client: Rc<APIClient<C>>,
}

impl<C: Connect> DockerClient<C> {
    pub fn new(client: APIClient<C>) -> DockerClient<C> {
        DockerClient {
            client: Rc::new(client),
        }
    }
}

impl<C: Connect> Deref for DockerClient<C> {
    type Target = APIClient<C>;

    fn deref(&self) -> &APIClient<C> {
        self.client.as_ref()
    }
}

impl<C: Connect> Clone for DockerClient<C> {
    fn clone(&self) -> Self {
        DockerClient {
            client: self.client.clone(),
        }
    }
}
