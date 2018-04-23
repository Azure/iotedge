// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Module, ModuleRegistry, ModuleRuntime, ModuleStatus};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use failure::ResultExt;
use futures::{future, Future, Stream};
use hyper::{Error as HyperError, StatusCode};
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
use management::models::*;
use serde::Serialize;
use serde::de::DeserializeOwned;
use serde_json;

use error::{Error, ErrorKind};
use IntoResponse;
use super::spec_to_core;

pub struct CreateModule<M>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
{
    runtime: M,
}

impl<M> CreateModule<M>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
{
    pub fn new(runtime: M) -> Self {
        CreateModule { runtime }
    }
}

impl<M> Handler<Parameters> for CreateModule<M>
where
    M: 'static + ModuleRuntime + Clone,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
    M::Error: IntoResponse,
    <M::ModuleRegistry as ModuleRegistry>::Error: IntoResponse,
{
    fn handle(&self, req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let runtime = self.runtime.clone();
        let response = req.body()
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
                        let created = runtime
                            .registry()
                            .pull(core_spec.config())
                            .and_then(move |_| {
                                runtime
                                    .create(core_spec)
                                    .map(move |_| {
                                        let details = spec_to_details(&spec);
                                        serde_json::to_string(&details)
                                            .context(ErrorKind::Serde)
                                            .map(|b| {
                                                Response::new()
                                                    .with_status(StatusCode::Created)
                                                    .with_header(ContentLength(b.len() as u64))
                                                    .with_header(ContentType::json())
                                                    .with_body(b)
                                            })
                                            .unwrap_or_else(|e| e.into_response())
                                    })
                                    .or_else(|e| future::ok(e.into_response()))
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

fn spec_to_details(spec: &ModuleSpec) -> ModuleDetails {
    let id = spec.name().clone();
    let name = spec.name().clone();
    let type_ = spec.type_().clone();

    let env = spec.config().env().map(|e| {
        e.iter()
            .map(|ev| EnvVar::new(ev.key().clone(), ev.value().clone()))
            .collect()
    });
    let mut config = Config::new(spec.config().settings().clone());
    if let Some(e) = env {
        config.set_env(e);
    }

    let runtime_status = RuntimeStatus::new(ModuleStatus::Created.to_string());
    let status = Status::new(runtime_status);
    ModuleDetails::new(id, name, type_, config, status)
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use chrono::prelude::*;
    use edgelet_core::{ModuleRuntimeState, ModuleStatus};
    use edgelet_http::route::Parameters;
    use hyper::{Method, Uri};
    use hyper::server::Request;
    use management::models::{Config, ErrorResponse};
    use server::module::tests::Error;
    use server::module::tests::*;

    use super::*;

    lazy_static! {
        static ref RUNTIME: TestRuntime = {
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
        let handler = CreateModule::new(RUNTIME.clone());
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let spec = ModuleSpec::new("test-module".to_string(), "docker".to_string(), config);
        let mut request = Request::new(
            Method::Post,
            Uri::from_str("http://localhost/modules").unwrap(),
        );
        request.set_body(serde_json::to_string(&spec).unwrap());

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::Created, response.status());
        assert_eq!(
            ContentLength(160),
            *response.headers().get::<ContentLength>().unwrap()
        );
        assert_eq!(
            ContentType::json(),
            *response.headers().get::<ContentType>().unwrap()
        );
        response
            .body()
            .concat2()
            .and_then(|b| {
                let details: ModuleDetails = serde_json::from_slice(&b).unwrap();
                assert_eq!("test-module", details.name());
                assert_eq!("docker", details.type_());
                assert_eq!(
                    "microsoft/test-image",
                    details.config().settings().get("image").unwrap()
                );
                assert_eq!("created", details.status().runtime_status().status());

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
        let mut request = Request::new(
            Method::Post,
            Uri::from_str("http://localhost/modules").unwrap(),
        );
        request.set_body(body);

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BadRequest, response.status());
        response
            .body()
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
        let handler = CreateModule::new(runtime);
        let config = Config::new(json!({"image":"microsoft/test-image"}));
        let spec = ModuleSpec::new("image-id".to_string(), "docker".to_string(), config);
        let mut request = Request::new(
            Method::Post,
            Uri::from_str("http://localhost/modules").unwrap(),
        );
        request.set_body(serde_json::to_string(&spec).unwrap());

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::InternalServerError, response.status());
        response
            .body()
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
        let handler = CreateModule::new(runtime);
        let config = Config::new(json!({}));
        let spec = ModuleSpec::new("image-id".to_string(), "docker".to_string(), config);
        let mut request = Request::new(
            Method::Post,
            Uri::from_str("http://localhost/modules").unwrap(),
        );
        request.set_body(serde_json::to_string(&spec).unwrap());

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BadRequest, response.status());
        response
            .body()
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
