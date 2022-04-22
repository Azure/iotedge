// Copyright (c) Microsoft. All rights reserved.

use anyhow::Context;
use futures::future::Either;
use futures::{Future, Stream};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use serde::de::DeserializeOwned;
use serde::Serialize;

use edgelet_core::{ImagePullPolicy, Module, ModuleRegistry, ModuleRuntime};
use edgelet_http::route::{Handler, Parameters};

use super::spec_to_core;
use crate::error::Error;

pub struct PrepareUpdateModule<M> {
    runtime: M,
}

impl<M> PrepareUpdateModule<M> {
    pub fn new(runtime: M) -> Self {
        PrepareUpdateModule { runtime }
    }
}

impl<M> Handler<Parameters> for PrepareUpdateModule<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
{
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        let runtime = self.runtime.clone();

        let response = req
            .into_body()
            .concat2()
            .then(|b| -> anyhow::Result<_> {
                let b = b.context(Error::MalformedRequestBody)?;
                let spec = serde_json::from_slice(&b).context(Error::MalformedRequestBody)?;
                let core_spec = spec_to_core::<M>(&spec)?;
                Ok((core_spec, runtime))
            })
            .and_then(|(core_spec, runtime)| {
                let name = core_spec.name().to_string();
                let image_pull_policy = core_spec.image_pull_policy();
                match image_pull_policy {
                    ImagePullPolicy::OnCreate => Either::A(
                        runtime
                            .registry()
                            .pull(core_spec.config())
                            .then(move |result| {
                                result.with_context(|| {
                                    Error::PrepareUpdateModule(name.clone())
                                })?;
                                Ok((name, true))
                            }),
                    ),
                    ImagePullPolicy::Never => Either::B(futures::future::ok((name, false))),
                }
            })
            .and_then(|(name, image_pulled)| -> anyhow::Result<_> {
                if image_pulled {
                    debug!("Successfully pulled new image for module {}", name)
                } else {
                    debug!(
                        "Skipped pulling image for module {} as per pull policy",
                        name
                    )
                }

                let response = Response::builder()
                    .status(StatusCode::NO_CONTENT)
                    .body(Body::default())
                    .context(Error::PrepareUpdateModule(name))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.downcast::<Error>().map_or_else(edgelet_http::error::catchall_error_response, Into::into)));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use chrono::prelude::*;
    use edgelet_core::{MakeModuleRuntime, ModuleAction, ModuleRuntimeState, ModuleStatus};
    use edgelet_http::route::Parameters;
    use edgelet_test_utils::module::{
        TestConfig, TestModule, TestRegistry, TestRuntime, TestSettings,
    };
    use futures::sync::mpsc;
    use lazy_static::lazy_static;
    use management::models::{Config, ErrorResponse, ModuleSpec};
    use serde_json::json;

    use super::{Future, Handler, PrepareUpdateModule, Request, StatusCode, Stream};
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
            let (create_socket_channel_snd, _create_socket_channel_rcv) =
                mpsc::unbounded::<ModuleAction>();

            TestRuntime::make_runtime(TestSettings::new(), create_socket_channel_snd)
                .wait()
                .unwrap()
                .with_module(Ok(module))
        };
    }

    #[test]
    fn success() {
        let handler = PrepareUpdateModule::new(RUNTIME.clone());
        let config = Config::new(json!({"image":"microsoft/test-image-2"}));
        let mut spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        spec.set_image_pull_policy("never".to_string());
        let request = Request::post("http://localhost/modules/test-module/prepareupdate")
            .body(serde_json::to_string(&spec).unwrap().into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::NO_CONTENT, response.status());
    }

    #[test]
    fn bad_body() {
        let handler = PrepareUpdateModule::new(RUNTIME.clone());
        let body = "invalid";
        let request = Request::post("http://localhost/modules/test-module/prepareupdate")
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
        let (create_socket_channel_snd, _create_socket_channel_rcv) =
            mpsc::unbounded::<ModuleAction>();

        let runtime = TestRuntime::make_runtime(TestSettings::new(), create_socket_channel_snd)
            .wait()
            .unwrap()
            .with_registry(TestRegistry::new(Some(Error::General)));
        let handler = PrepareUpdateModule::new(runtime);
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        let request = Request::post("http://localhost/modules/test-module/prepareupdate")
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
                    "Could not prepare update for module \"test-module\"\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn bad_settings() {
        let (create_socket_channel_snd, _create_socket_channel_rcv) =
            mpsc::unbounded::<ModuleAction>();

        let runtime = TestRuntime::make_runtime(TestSettings::new(), create_socket_channel_snd)
            .wait()
            .unwrap()
            .with_module(Err(Error::General));
        let handler = PrepareUpdateModule::new(runtime);
        let config = Config::new(json!({}));
        let spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        let request = Request::put("http://localhost/modules/test-module")
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
        let handler = PrepareUpdateModule::new(RUNTIME.clone());
        let config = Config::new(json!({"image":"microsoft/test-image-2"}));
        let mut spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        spec.set_image_pull_policy("what".to_string());
        let request = Request::post("http://localhost/modules/test-module/prepareupdate")
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
                assert_eq!("Request body is malformed\n\tcaused by: Invalid image pull policy configuration \"what\"", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
