// Copyright (c) Microsoft. All rights reserved.

use std::path::Path;

use hyper::Client;
use hyperlocal::{UnixConnector, Uri};
use tokio_core::reactor::Handle;
use url::Url;

use docker_rs::apis::client::APIClient;
use docker_rs::apis::configuration::Configuration;

use error::{Error, ErrorKind};

pub struct DockerModuleRuntime {
    // TODO: Rename this field to remove leading underscore once it is
    //       used. Currently this suppresses the "Unused" warning.
    _client: APIClient<UnixConnector>,
}

impl DockerModuleRuntime {
    pub fn new(docker_uri: &Url, handle: &Handle) -> Result<DockerModuleRuntime, Error> {
        // we assume "docker_uri" is a unix domain socket for now
        if let Some(err) = DockerModuleRuntime::validate_uds_uri(docker_uri) {
            return Err(err);
        }

        // build the hyper client
        let client = Client::configure()
            .connector(UnixConnector::new(handle.clone()))
            .build(handle);

        // extract base path - the bit that comes after unix://
        let base_path = docker_uri.path();
        let mut configuration = Configuration::new(client);
        configuration.base_path = base_path.to_string();
        configuration.uri_composer =
            Box::new(|base_path, path| Ok(Uri::new(base_path, path).into()));

        Ok(DockerModuleRuntime {
            _client: APIClient::new(configuration),
        })
    }

    fn validate_uds_uri(docker_uri: &Url) -> Option<Error> {
        if docker_uri.scheme() != "unix" || !Path::new(docker_uri.path()).exists() {
            Some(Error::from(ErrorKind::InvalidUdsUri(
                docker_uri.to_string(),
            )))
        } else {
            None
        }
    }
}

#[cfg(test)]
mod tests {
    use tokio_core::reactor::Core;
    use tempfile::NamedTempFile;
    use url::Url;

    use runtime::DockerModuleRuntime;

    #[test]
    #[should_panic(expected = "Invalid Unix domain socket URI")]
    fn invalid_uri_prefix_fails() {
        let core = Core::new().unwrap();
        let _mri = DockerModuleRuntime::new(
            &Url::parse("foo:///this/is/not/valid").unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[test]
    #[should_panic(expected = "Invalid Unix domain socket URI")]
    fn invalid_uds_path_fails() {
        let core = Core::new().unwrap();
        let _mri = DockerModuleRuntime::new(
            &Url::parse("unix:///this/file/does/not/exist").unwrap(),
            &core.handle(),
        ).unwrap();
    }

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
