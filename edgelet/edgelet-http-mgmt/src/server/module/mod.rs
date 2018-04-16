// Copyright (c) Microsoft. All rights reserved.

use failure::{Fail, ResultExt};
use futures::Future;
use hyper::StatusCode;
use hyper::server::Response;
use serde::Serialize;
use serde_json;

use docker_mri::{Error as DockerError, ErrorKind as DockerErrorKind};
use edgelet_core::Module;
use hyper::header::{ContentLength, ContentType};
use management::models::*;

use error::{Error, ErrorKind};
use IntoResponse;

mod create;
mod delete;
mod get;
mod list;
mod restart;
mod start;
mod stop;
mod update;

pub use self::create::CreateModule;
pub use self::delete::DeleteModule;
pub use self::get::GetModule;
pub use self::list::ListModules;
pub use self::restart::RestartModule;
pub use self::start::StartModule;
pub use self::stop::StopModule;
pub use self::update::UpdateModule;

impl IntoResponse for DockerError {
    fn into_response(self) -> Response {
        let mut fail: &Fail = &self;
        let mut message = self.to_string();
        while let Some(cause) = fail.cause() {
            message.push_str(&format!("\n\tcaused by: {}", cause.to_string()));
            fail = cause;
        }

        let status_code = match *self.kind() {
            DockerErrorKind::NotFound => StatusCode::NotFound,
            DockerErrorKind::Conflict => StatusCode::Conflict,
            DockerErrorKind::NotModified => StatusCode::NotModified,
            _ => StatusCode::InternalServerError,
        };

        // Per the RFC, status code NotModified should not have a body
        let body = if status_code != StatusCode::NotModified {
            let b = serde_json::to_string(&ErrorResponse::new(message))
                .expect("serialization of ErrorResponse failed.");
            Some(b)
        } else {
            None
        };

        body.map(|b| {
            Response::new()
                .with_status(status_code)
                .with_header(ContentLength(b.len() as u64))
                .with_header(ContentType::json())
                .with_body(b)
        }).unwrap_or_else(|| Response::new().with_status(status_code))
    }
}

fn core_to_details<M>(module: M) -> Box<Future<Item = ModuleDetails, Error = Error>>
where
    M: 'static + Module,
    M::Config: Serialize,
{
    let details = module
        .runtime_state()
        .then(move |result| {
            result.context(ErrorKind::ModuleRuntime).and_then(|state| {
                serde_json::to_value(module.config())
                    .context(ErrorKind::Serde)
                    .map(|settings| {
                        let config = Config::new(settings).with_env(Vec::new());
                        let mut runtime_status = RuntimeStatus::new(state.status().to_string());
                        state
                            .status_description()
                            .map(|d| runtime_status.set_description(d.to_string()));
                        let mut status = Status::new(runtime_status);
                        state
                            .started_at()
                            .map(|s| status.set_start_time(s.to_rfc3339()));
                        state.exit_code().and_then(|code| {
                            state.finished_at().map(|f| {
                                status.set_exit_status(ExitStatus::new(
                                    f.to_rfc3339(),
                                    code.to_string(),
                                ))
                            })
                        });

                        ModuleDetails::new(
                            "id".to_string(),
                            module.name().to_string(),
                            module.type_().to_string(),
                            config,
                            status,
                        )
                    })
            })
        })
        .map_err(From::from);
    Box::new(details)
}

#[cfg(test)]
mod tests {
    use docker_mri::{Error as DockerError, ErrorKind as DockerErrorKind};
    use edgelet_core::*;
    use futures::{Future, IntoFuture, Stream};
    use futures::future::{self, FutureResult};
    use hyper::StatusCode;
    use hyper::server::Response;
    use management::models::ErrorResponse;
    use serde_json;

    use IntoResponse;

    #[derive(Clone, Debug, Fail)]
    pub enum Error {
        #[fail(display = "General error")]
        General,
    }

    impl IntoResponse for Error {
        fn into_response(self) -> Response {
            let body = serde_json::to_string(&ErrorResponse::new(self.to_string()))
                .expect("serialization of ErrorResponse failed.");
            Response::new()
                .with_status(StatusCode::InternalServerError)
                .with_body(body)
        }
    }

    #[derive(Clone, Debug)]
    pub struct NullRegistry;

    impl ModuleRegistry for NullRegistry {
        type Error = Error;
        type PullFuture = FutureResult<(), Self::Error>;
        type RemoveFuture = FutureResult<(), Self::Error>;
        type RegistryAuthConfig = ();

        fn pull(
            &mut self,
            _name: &str,
            _credentials: Option<&Self::RegistryAuthConfig>,
        ) -> Self::PullFuture {
            future::ok(())
        }

        fn remove(&mut self, _name: &str) -> Self::RemoveFuture {
            future::ok(())
        }
    }

    #[derive(Clone, Debug, Serialize, Deserialize)]
    pub struct TestConfig {
        image: String,
    }

    impl TestConfig {
        pub fn new(image: String) -> Self {
            TestConfig { image }
        }

        pub fn image(&self) -> &str {
            &self.image
        }
    }

    #[derive(Clone, Debug)]
    pub struct TestModule {
        name: String,
        config: TestConfig,
        state: Result<ModuleRuntimeState, Error>,
    }

    impl TestModule {
        pub fn new(
            name: String,
            config: TestConfig,
            state: Result<ModuleRuntimeState, Error>,
        ) -> Self {
            TestModule {
                name,
                config,
                state,
            }
        }
    }

    impl Module for TestModule {
        type Config = TestConfig;
        type Error = Error;
        type RuntimeStateFuture = FutureResult<ModuleRuntimeState, Self::Error>;

        fn name(&self) -> &str {
            &self.name
        }

        fn type_(&self) -> &str {
            "test"
        }

        fn config(&self) -> &Self::Config {
            &self.config
        }

        fn runtime_state(&self) -> Self::RuntimeStateFuture {
            self.state.clone().into_future()
        }
    }

    #[derive(Clone)]
    pub struct TestRuntime {
        module: Result<TestModule, Error>,
        registry: NullRegistry,
    }

    impl TestRuntime {
        pub fn new(module: Result<TestModule, Error>) -> Self {
            TestRuntime {
                module,
                registry: NullRegistry,
            }
        }
    }

    impl ModuleRuntime for TestRuntime {
        type Error = Error;
        type Config = TestConfig;
        type Module = TestModule;
        type ModuleRegistry = NullRegistry;
        type CreateFuture = FutureResult<(), Self::Error>;
        type StartFuture = FutureResult<(), Self::Error>;
        type StopFuture = FutureResult<(), Self::Error>;
        type RestartFuture = FutureResult<(), Self::Error>;
        type RemoveFuture = FutureResult<(), Self::Error>;
        type ListFuture = FutureResult<Vec<Self::Module>, Self::Error>;

        fn create(&mut self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
            match self.module {
                Ok(_) => future::ok(()),
                Err(ref e) => future::err(e.clone()),
            }
        }

        fn start(&mut self, _id: &str) -> Self::StartFuture {
            match self.module {
                Ok(_) => future::ok(()),
                Err(ref e) => future::err(e.clone()),
            }
        }

        fn stop(&mut self, _id: &str) -> Self::StopFuture {
            match self.module {
                Ok(_) => future::ok(()),
                Err(ref e) => future::err(e.clone()),
            }
        }

        fn restart(&mut self, _id: &str) -> Self::RestartFuture {
            match self.module {
                Ok(_) => future::ok(()),
                Err(ref e) => future::err(e.clone()),
            }
        }

        fn remove(&mut self, _id: &str) -> Self::RemoveFuture {
            match self.module {
                Ok(_) => future::ok(()),
                Err(ref e) => future::err(e.clone()),
            }
        }

        fn list(&self) -> Self::ListFuture {
            match self.module {
                Ok(ref m) => future::ok(vec![m.clone()]),
                Err(ref e) => future::err(e.clone()),
            }
        }

        fn registry_mut(&mut self) -> &mut Self::ModuleRegistry {
            &mut self.registry
        }
    }

    #[test]
    fn not_found() {
        // arrange
        let error = DockerError::from(DockerErrorKind::NotFound);

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::NotFound, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!("Container not found", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn conflict() {
        // arrange
        let error = DockerError::from(DockerErrorKind::Conflict);

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::Conflict, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!("Conflict with current operation", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn internal_server() {
        // arrange
        let error = DockerError::from(DockerErrorKind::UrlParse);

        // act
        let response = error.into_response();

        // assert
        assert_eq!(StatusCode::InternalServerError, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!("Invalid URL", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
