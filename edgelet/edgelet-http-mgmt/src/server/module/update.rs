// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Module, ModuleRegistry, ModuleRuntime, ModuleStatus};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use failure::ResultExt;
use futures::{future, Future, Stream};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use management::models::*;
use serde::de::DeserializeOwned;
use serde::Serialize;
use serde_json;
use url::form_urlencoded::parse as parse_query;

use super::{spec_to_core, spec_to_details};
use error::{Error, ErrorKind};
use IntoResponse;

pub struct UpdateModule<M>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
{
    runtime: M,
}

impl<M> UpdateModule<M>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
{
    pub fn new(runtime: M) -> Self {
        UpdateModule { runtime }
    }
}

impl<M> Handler<Parameters> for UpdateModule<M>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
    M::Error: IntoResponse,
    <M::ModuleRegistry as ModuleRegistry>::Error: IntoResponse,
{
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        let runtime = self.runtime.clone();
        let start: bool = req.uri()
            .query()
            .and_then(|query| {
                parse_query(query.as_bytes())
                    .find(|&(ref key, _)| key == "start")
                    .and_then(|(_, v)| if v != "false" { Some(()) } else { None })
                    .map(|_| true)
            })
            .unwrap_or_else(|| false);

        let response = req.into_body()
            .concat2()
            .and_then(move |b| {
                serde_json::from_slice::<ModuleSpec>(&b)
                    .context(ErrorKind::BadBody)
                    .map_err(From::from)
                    .and_then(|spec| {
                        spec_to_core::<M>(&spec)
                            .context(ErrorKind::BadBody)
                            .map_err(Error::from)
                            .map(|core_spec| (core_spec, spec))
                    })
                    .map(move |(core_spec, spec)| {
                        let name = core_spec.name().to_string();

                        if start {
                            info!("Updating and starting module {}", name);
                        } else {
                            info!("Updating module {}", name);
                        }

                        let created = runtime
                            .remove(&name)
                            .and_then(move |_| {
                                debug!("Removed existing module {}", name);
                                runtime
                                    .registry()
                                    .pull(core_spec.config())
                                    .and_then(move |_| {
                                        debug!("Successfully pulled new image for module {}", name);
                                        runtime.create(core_spec).and_then(move |_| {
                                            debug!("Created module {}", name);
                                            if start {
                                                info!("Starting module {}", name);
                                                future::Either::A(
                                                    runtime
                                                        .start(&name)
                                                        .map(|_| ModuleStatus::Running),
                                                )
                                            } else {
                                                future::Either::B(future::ok(ModuleStatus::Stopped))
                                            }.map(
                                                move |status| {
                                                    let details = spec_to_details(&spec, &status);
                                                    serde_json::to_string(&details)
                                                        .context(ErrorKind::Serde)
                                                        .map(|b| {
                                                            Response::builder()
                                                                .status(StatusCode::OK)
                                                                .header(
                                                                    CONTENT_TYPE,
                                                                    "application/json",
                                                                )
                                                                .header(
                                                                    CONTENT_LENGTH,
                                                                    b.len().to_string().as_str(),
                                                                )
                                                                .body(b.into())
                                                                .unwrap_or_else(|e| {
                                                                    e.into_response()
                                                                })
                                                        })
                                                        .unwrap_or_else(|e| e.into_response())
                                                },
                                            )
                                        })
                                    })
                            })
                            .or_else(|e| future::ok(e.into_response()));
                        future::Either::A(created)
                    })
                    .unwrap_or_else(|e| future::Either::B(future::ok(e.into_response())))
            })
            .or_else(|e| future::ok(e.into_response()));
        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use chrono::prelude::*;
    use edgelet_core::{ModuleRuntimeState, ModuleStatus};
    use edgelet_http::route::Parameters;
    use edgelet_test_utils::module::*;
    use management::models::{Config, ErrorResponse};
    use server::module::tests::Error;

    use super::*;

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
        let spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
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
                let expected = "Bad body\n\tcaused by: expected value at line 1 column 1";
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
                assert_eq!("General error", error.message());
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
                    "Bad body\n\tcaused by: Serde error\n\tcaused by: missing field `image`",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
