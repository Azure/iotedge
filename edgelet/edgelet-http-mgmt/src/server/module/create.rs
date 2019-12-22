// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::future::Either;
use futures::{Future, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use serde::de::DeserializeOwned;
use serde::Serialize;
use serde_json;

use edgelet_core::{
    ImagePullPolicy, Module, ModuleRegistry, ModuleRuntime, ModuleStatus, RuntimeOperation,
};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use management::models::*;

use super::{spec_to_core, spec_to_details};
use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct CreateModule<M> {
    runtime: M,
}

impl<M> CreateModule<M> {
    pub fn new(runtime: M) -> Self {
        CreateModule { runtime }
    }
}

impl<M> Handler<Parameters> for CreateModule<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
{
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let runtime = self.runtime.clone();
        let response = req
            .into_body()
            .concat2()
            .then(|b| {
                let b = b.context(ErrorKind::MalformedRequestBody)?;
                let spec = serde_json::from_slice::<ModuleSpec>(&b)
                    .context(ErrorKind::MalformedRequestBody)?;
                let core_spec = spec_to_core::<M>(&spec, ErrorKind::MalformedRequestBody)?;
                Ok((spec, core_spec))
            })
            .and_then(move |(spec, core_spec)| {
                let module_name = spec.name().to_string();
                let image_pull_policy = core_spec.image_pull_policy();

                let pull_future = match image_pull_policy {
                    ImagePullPolicy::OnCreate => Either::A(
                        runtime
                            .registry()
                            .pull(core_spec.config())
                            .then(move |result| {
                                result.with_context(|_| {
                                    ErrorKind::RuntimeOperation(RuntimeOperation::CreateModule(
                                        module_name.clone(),
                                    ))
                                })?;
                                Ok((module_name, true))
                            }),
                    ),
                    ImagePullPolicy::Never => Either::B(futures::future::ok((module_name, false))),
                };

                pull_future.and_then(move |(name, image_pulled)| -> Result<_, Error> {
                    if image_pulled {
                        debug!("Successfully pulled new image for module {}", name)
                    } else {
                        debug!(
                            "Skipped pulling image for module {} as per pull policy",
                            name
                        )
                    }

                    Ok(runtime
                        .create(core_spec)
                        .then(move |result| -> Result<_, Error> {
                            result.with_context(|_| {
                                ErrorKind::RuntimeOperation(RuntimeOperation::CreateModule(
                                    name.clone(),
                                ))
                            })?;
                            let details = spec_to_details(&spec, ModuleStatus::Stopped);
                            let b = serde_json::to_string(&details).with_context(|_| {
                                ErrorKind::RuntimeOperation(RuntimeOperation::CreateModule(
                                    name.clone(),
                                ))
                            })?;
                            let response = Response::builder()
                                .status(StatusCode::CREATED)
                                .header(CONTENT_TYPE, "application/json")
                                .header(CONTENT_LENGTH, b.len().to_string().as_str())
                                .body(b.into())
                                .context(ErrorKind::RuntimeOperation(
                                    RuntimeOperation::CreateModule(name),
                                ))?;
                            Ok(response)
                        }))
                })
            })
            .flatten()
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use chrono::prelude::*;
    use hyper::Request;
    use lazy_static::lazy_static;
    use serde_json::json;

    use edgelet_core::{MakeModuleRuntime, ModuleRuntimeState, ModuleStatus};
    use edgelet_http::route::Parameters;
    use edgelet_test_utils::crypto::TestHsm;
    use edgelet_test_utils::module::*;
    use management::models::{Config, ErrorResponse};

    use super::*;
    use crate::server::module::tests::Error;

    lazy_static! {
        static ref RUNTIME: TestRuntime<Error, TestSettings> = {
            let state = ModuleRuntimeState::default()
                .with_status(ModuleStatus::Running)
                .with_exit_code(Some(0))
                .with_status_description(Some("description".to_string()))
                .with_started_at(Some(Utc.ymd(2018, 4, 13).and_hms_milli(14, 20, 0, 1)))
                .with_finished_at(Some(Utc.ymd(2018, 4, 13).and_hms_milli(15, 20, 0, 1)))
                .with_image_id(Some("image-id".to_string()));
            let config = TestConfig::new("microsoft/test-image".to_string());
            let module = TestModule::new("test-module".to_string(), config, Ok(state));
            TestRuntime::make_runtime(
                TestSettings::new(),
                TestProvisioningResult::new(),
                TestHsm::default(),
            )
            .wait()
            .unwrap()
            .with_module(Ok(module))
        };
    }

    #[test]
    fn success() {
        let handler = CreateModule::new(RUNTIME.clone());
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let mut spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        spec.set_image_pull_policy("on-create".to_string());
        let request = Request::post("http://localhost/modules")
            .body(serde_json::to_string(&spec).unwrap().into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::CREATED, response.status());
        assert_eq!("160", *response.headers().get(CONTENT_LENGTH).unwrap());
        assert_eq!(
            "application/json",
            *response.headers().get(CONTENT_TYPE).unwrap()
        );
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let details: ModuleDetails = serde_json::from_slice(&b).unwrap();
                assert_eq!("test-module", details.name());
                assert_eq!("docker", details.type_());
                assert_eq!(
                    "microsoft/test-image",
                    details.config().settings().get("image").unwrap()
                );
                assert_eq!("stopped", details.status().runtime_status().status());

                assert_eq!(160, b.len());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn bad_body() {
        let handler = CreateModule::new(RUNTIME.clone());
        let body = "invalid";
        let request = Request::post("http://localhost/modules")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected =
                    "Request body is malformed\n\tcaused by: expected value at line 1 column 1";
                assert_eq!(expected, error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn runtime_error() {
        let runtime = TestRuntime::make_runtime(
            TestSettings::new(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
        .wait()
        .unwrap()
        .with_module(Err(Error::General));
        let handler = CreateModule::new(runtime);
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let spec = ModuleSpec::new("image-id".to_string(), "docker".to_string(), config);
        let request = Request::post("http://localhost/modules")
            .body(serde_json::to_string(&spec).unwrap().into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Could not create module image-id\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn bad_settings() {
        let runtime = TestRuntime::make_runtime(
            TestSettings::new(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
        .wait()
        .unwrap()
        .with_module(Err(Error::General));
        let handler = CreateModule::new(runtime);
        let config = Config::new(json!({}));
        let spec = ModuleSpec::new("image-id".to_string(), "docker".to_string(), config);
        let request = Request::post("http://localhost/modules")
            .body(serde_json::to_string(&spec).unwrap().into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Request body is malformed\n\tcaused by: missing field `image`",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn bad_image_pull_policy() {
        let handler = CreateModule::new(RUNTIME.clone());
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let mut spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        spec.set_image_pull_policy("what".to_string());
        let request = Request::post("http://localhost/modules")
            .body(serde_json::to_string(&spec).unwrap().into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected = "Request body is malformed\n\tcaused by: Invalid image pull policy configuration \"what\"";
                assert_eq!(expected, error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
