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
        let base_path = get_base_path(docker_url);
        let mut configuration = Configuration::new(client);
        configuration.base_path = base_path.to_string();
        configuration.uri_composer = Box::new(|base_path, path| {
            // TODO: We are using `unwrap` here instead of `map_err`
            //       because `hyper::error::UriError` cannot be
            //       instantiated (it relies on private types). We may
            //       be able to fix this by changing the definition of
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

fn get_base_path(url: &Url) -> &str {
    match url.scheme() {
        "unix" => url.path(),
        _ => url.as_str(),
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
    use std::mem;

    use futures::prelude::*;
    #[cfg(unix)]
    use tempfile::NamedTempFile;
    use tokio_core::reactor::Core;
    use url::Url;

    use edgelet_core::ModuleRegistry;
    use edgelet_utils::{Error as UtilsError, ErrorKind as UtilsErrorKind};

    use error::{Error, ErrorKind};
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
    fn create_with_uds_succeeds() {
        let core = Core::new().unwrap();
        let file = NamedTempFile::new().unwrap();
        let file_path = file.path().to_str().unwrap();
        let _mri = DockerModuleRuntime::new(
            &Url::parse(&format!("unix://{}", file_path)).unwrap(),
            &core.handle(),
        ).unwrap();
    }

    fn empty_test<F, R>(tester: F)
    where
        F: Fn(&mut DockerModuleRuntime) -> R,
        R: Future<Item = (), Error = Error>,
    {
        let mut core = Core::new().unwrap();
        let mut mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = tester(&mut mri).then(|res| match res {
            Ok(_) => Err("Expected error but got a result.".to_string()),
            Err(err) => {
                let utils_error = UtilsError::from(UtilsErrorKind::ArgumentEmpty("".to_string()));
                if mem::discriminant(err.kind())
                    == mem::discriminant(&ErrorKind::Utils(utils_error))
                {
                    Ok(())
                } else {
                    Err(format!(
                        "Wrong error kind. Expected `ArgumentEmpty` found {:?}",
                        err
                    ))
                }
            }
        });

        core.run(task).unwrap();
    }

    #[test]
    fn image_pull_with_empty_name_fails() {
        empty_test(|ref mut mri| mri.pull(""));
    }

    #[test]
    fn image_pull_with_white_space_name_fails() {
        empty_test(|ref mut mri| mri.pull("     "));
    }

    #[test]
    fn image_remove_with_empty_name_fails() {
        empty_test(|ref mut mri| mri.remove(""));
    }

    #[test]
    fn image_remove_with_white_space_name_fails() {
        empty_test(|ref mut mri| mri.remove("     "));
    }
}
