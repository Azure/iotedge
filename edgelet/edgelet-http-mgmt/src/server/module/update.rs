// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::future::Either;
use futures::{future, Future, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use log::info;
use serde::de::DeserializeOwned;
use serde::Serialize;
use serde_json;
use url::form_urlencoded::parse as parse_query;

use edgelet_core::{ImagePullPolicy, Module, ModuleRegistry, ModuleRuntime, ModuleStatus};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;

use super::{spec_to_core, spec_to_details};
use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct UpdateModule<M> {
    runtime: M,
}

impl<M> UpdateModule<M> {
    pub fn new(runtime: M) -> Self {
        UpdateModule { runtime }
    }
}

impl<M> Handler<Parameters> for UpdateModule<M>
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

        let start: bool = req
            .uri()
            .query()
            .and_then(|query| {
                parse_query(query.as_bytes())
                    .find(|&(ref key, _)| key == "start")
                    .and_then(|(_, v)| if v == "false" { None } else { Some(()) })
                    .map(|_| true)
            })
            .unwrap_or_else(|| false);

        let response = req
            .into_body()
            .concat2()
            .then(|b| -> Result<_, Error> {
                let b = b.context(ErrorKind::MalformedRequestBody)?;
                let spec = serde_json::from_slice(&b).context(ErrorKind::MalformedRequestBody)?;
                let core_spec = spec_to_core::<M>(&spec, ErrorKind::MalformedRequestBody)?;
                Ok((core_spec, spec))
            })
            .and_then(move |(core_spec, spec)| {
                let name = core_spec.name().to_string();

                if start {
                    info!("Updating and starting module {}", name);
                } else {
                    info!("Updating module {}", name);
                }

                runtime.remove(&name).then(|result| {
                    result.with_context(|_| ErrorKind::UpdateModule(name.clone()))?;
                    Ok((core_spec, spec, name, runtime))
                })
            })
            .and_then(|(core_spec, spec, name, runtime)| {
                debug!("Removed existing module {}", name);

                match core_spec.image_pull_policy() {
                    ImagePullPolicy::OnCreate => {
                        Either::A(runtime.registry().pull(core_spec.config()).then(|result| {
                            result.with_context(|_| ErrorKind::UpdateModule(name.clone()))?;
                            Ok((core_spec, spec, name, runtime, true))
                        }))
                    }
                    ImagePullPolicy::Never => {
                        Either::B(futures::future::ok((core_spec, spec, name, runtime, false)))
                    }
                }
            })
            .and_then(|(core_spec, spec, name, runtime, image_pulled)| {
                if image_pulled {
                    debug!("Successfully pulled new image for module {}", name)
                } else {
                    debug!(
                        "Skipped pulling image for module {} as per pull policy",
                        name
                    )
                }

                runtime.create(core_spec).then(|result| {
                    result.with_context(|_| ErrorKind::UpdateModule(name.clone()))?;
                    Ok((name, spec, runtime))
                })
            })
            .and_then(move |(name, spec, runtime)| {
                debug!("Created module {}", name);
                if start {
                    info!("Starting module {}", name);
                    future::Either::A(runtime.start(&name).then(|result| {
                        result.with_context(|_| ErrorKind::UpdateModule(name.clone()))?;
                        Ok((ModuleStatus::Running, spec, name))
                    }))
                } else {
                    future::Either::B(future::ok((ModuleStatus::Stopped, spec, name)))
                }
            })
            .and_then(|(status, spec, name)| -> Result<_, Error> {
                let details = spec_to_details(&spec, status);
                let b = serde_json::to_string(&details)
                    .with_context(|_| ErrorKind::UpdateModule(name.clone()))?;
                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, b.len().to_string().as_str())
                    .body(b.into())
                    .context(ErrorKind::UpdateModule(name))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use chrono::prelude::*;
    use edgelet_core::{ModuleRuntimeState, ModuleStatus};
    use edgelet_http::route::Parameters;
    use edgelet_test_utils::module::*;
    use lazy_static::lazy_static;
    use management::models::{Config, ErrorResponse, ModuleDetails, ModuleSpec};
    use serde_json::json;

    use super::*;
    use crate::server::module::tests::Error;

    lazy_static! {
        static ref RUNTIME: TestRuntime<Error> = {
            let state = ModuleRuntimeState::default()
                .with_status(ModuleStatus::Running)
                .with_exit_code(Some(0))
                .with_status_description(Some("description".to_string()))
                .with_started_at(Some(Utc.ymd(2018, 4, 13).and_hms_milli(14, 20, 0, 1)))
                .with_finished_at(Some(Utc.ymd(2018, 4, 13).and_hms_milli(15, 20, 0, 1)))
                .with_image_id(Some("image-id".to_string()));
            let config = TestConfig::new("microsoft/test-image".to_string());
            let module = TestModule::new("test-module".to_string(), config, Ok(state));
            TestRuntime::new(Ok(module))
        };
    }

    #[test]
    fn success() {
        let handler = UpdateModule::new(RUNTIME.clone());
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        let request = Request::put("http://localhost/modules/test-module")
            .body(serde_json::to_string(&spec).unwrap().into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::OK, response.status());
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
    fn success_start() {
        let handler = UpdateModule::new(RUNTIME.clone());
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let mut spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        spec.set_image_pull_policy("on-create".to_string());
        let request = Request::put("http://localhost/modules/test-module?start")
            .body(serde_json::to_string(&spec).unwrap().into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::OK, response.status());
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
                assert_eq!("running", details.status().runtime_status().status());

                assert_eq!(160, b.len());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn bad_body() {
        let handler = UpdateModule::new(RUNTIME.clone());
        let body = "invalid";
        let request = Request::put("http://localhost/modules/test-module")
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
        let runtime = TestRuntime::new(Err(Error::General));
        let handler = UpdateModule::new(runtime);
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        let request = Request::put("http://localhost/modules/test-module")
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
                    "Could not update module \"test-module\"\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn bad_settings() {
        let runtime = TestRuntime::new(Err(Error::General));
        let handler = UpdateModule::new(runtime);
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
        let handler = UpdateModule::new(RUNTIME.clone());
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let mut spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        spec.set_image_pull_policy("what".to_string());
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
                assert_eq!("Request body is malformed\n\tcaused by: Invalid image pull policy configuration \"what\"", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
