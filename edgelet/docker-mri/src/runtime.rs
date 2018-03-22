// Copyright (c) Microsoft. All rights reserved.

use futures::prelude::*;
use hyper::Client;
use tokio_core::reactor::Handle;
use url::Url;

use docker_rs::apis::client::APIClient;
use docker_rs::apis::configuration::Configuration;
use edgelet_core::ModuleRegistry;

use docker_connector::DockerConnector;
use error::{Error, ErrorKind};

pub struct DockerModuleRuntime {
    client: APIClient<DockerConnector>,
}

impl DockerModuleRuntime {
    pub fn new(docker_url: &Url, handle: &Handle) -> Result<DockerModuleRuntime, Error> {
        // build the hyper client
        let client = Client::configure()
            .connector(DockerConnector::new(docker_url, handle)?)
            .build(handle);

        // extract base path - the bit that comes after the scheme
        let base_path = docker_url.path();
        let mut configuration = Configuration::new(client);
        configuration.base_path = base_path.to_string();
        configuration.uri_composer = Box::new(|base_path, path| {
            // TODO: We are using `unwrap` here instead of `map_err`
            //       because `hyper::error::UriError` cannot be
            //       instantiated (it relies on private types). We may
            //       able to fix this by changing the definition of
            //       `uri_composer` in the `docker-rs` crate to return
            //       some other kind of error instead of `UriError`.
            Url::parse(base_path)
                .and_then(|base| base.join(path))
                .unwrap()
                .as_str()
                .parse()
        });

        Ok(DockerModuleRuntime {
            client: APIClient::new(configuration),
        })
    }
}

impl ModuleRegistry for DockerModuleRuntime {
    type Error = Error;
    type PullFuture = Box<Future<Item = (), Error = Self::Error>>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error>>;

    fn pull(&mut self, name: &str) -> Self::PullFuture {
        Box::new(
            self.client
                .image_api()
                .image_create(fensure_not_empty!(name), "", "", "", "", "", "")
                .map_err(|err| Error::from(ErrorKind::Docker(err))),
        )
    }

    fn remove(&mut self, name: &str) -> Self::RemoveFuture {
        Box::new(
            self.client
                .image_api()
                .image_delete(fensure_not_empty!(name), false, false)
                .map(|_| ())
                .map_err(|err| Error::from(ErrorKind::Docker(err))),
        )
    }
}

#[cfg(test)]
mod tests {
    #[cfg(unix)]
    use tempfile::NamedTempFile;
    use tokio_core::reactor::Core;
    use url::Url;

    use runtime::DockerModuleRuntime;

    #[test]
    #[should_panic(expected = "Invalid docker URI")]
    fn invalid_uri_prefix_fails() {
        let core = Core::new().unwrap();
        let _mri = DockerModuleRuntime::new(
            &Url::parse("foo:///this/is/not/valid").unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[cfg(unix)]
    #[test]
    #[should_panic(expected = "Invalid unix domain socket URI")]
    fn invalid_uds_path_fails() {
        let core = Core::new().unwrap();
        let _mri = DockerModuleRuntime::new(
            &Url::parse("unix:///this/file/does/not/exist").unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[cfg(unix)]
    #[test]
    fn create_succeeds() {
        let core = Core::new().unwrap();
        let file = NamedTempFile::new().unwrap();
        let file_path = file.path().to_str().unwrap();
        let _mri = DockerModuleRuntime::new(
            &Url::parse(&format!("unix://{}", file_path)).unwrap(),
            &core.handle(),
        ).unwrap();
    }
}
