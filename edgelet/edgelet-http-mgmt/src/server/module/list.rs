// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::{future, Future};
use hyper::{Error as HyperError, StatusCode};
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
use serde::Serialize;
use serde_json;

use edgelet_core::{Module, ModuleRuntime};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use management::models::*;

use error::ErrorKind;
use IntoResponse;
use super::core_to_details;

pub struct ListModules<M>
where
    M: 'static + ModuleRuntime,
    <M::Module as Module>::Config: Serialize,
{
    runtime: M,
}

impl<M> ListModules<M>
where
    M: 'static + ModuleRuntime,
    <M::Module as Module>::Config: Serialize,
{
    pub fn new(runtime: M) -> Self {
        ListModules { runtime }
    }
}

impl<M> Handler<Parameters> for ListModules<M>
where
    M: 'static + ModuleRuntime,
    <M::Module as Module>::Config: Serialize,
{
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        debug!("List modules");
        let response = self.runtime.list().then(|result| {
            match result.context(ErrorKind::ModuleRuntime) {
                Ok(mods) => {
                    let futures = mods.into_iter().map(core_to_details);
                    let response = future::join_all(futures)
                        .map(|details| {
                            let body = ModuleList::new(details);
                            serde_json::to_string(&body)
                                .context(ErrorKind::Serde)
                                .map(|b| {
                                    Response::new()
                                        .with_status(StatusCode::Ok)
                                        .with_header(ContentLength(b.len() as u64))
                                        .with_header(ContentType::json())
                                        .with_body(b)
                                })
                                .unwrap_or_else(|e| e.into_response())
                        })
                        .or_else(|e| future::ok(e.into_response()));
                    future::Either::A(response)
                }
                Err(e) => future::Either::B(future::ok(e.into_response())),
            }
        });
        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use chrono::prelude::*;
    use edgelet_core::{ModuleRuntimeState, ModuleStatus};
    use edgelet_http::route::Parameters;
    use futures::Stream;
    use hyper::{Method, Uri};
    use hyper::server::Request;
    use management::models::ModuleList;
    use server::module::tests::Error;
    use server::module::tests::*;

    use super::*;

    #[test]
    fn success() {
        // arrange
        let state = ModuleRuntimeState::default()
            .with_status(ModuleStatus::Running)
            .with_exit_code(Some(0))
            .with_status_description(Some("description".to_string()))
            .with_started_at(Some(Utc.ymd(2018, 4, 13).and_hms_milli(14, 20, 0, 1)))
            .with_finished_at(Some(Utc.ymd(2018, 4, 13).and_hms_milli(15, 20, 0, 1)))
            .with_image_id(Some("image-id".to_string()));
        let config = TestConfig::new("microsoft/test-image".to_string());
        let module = TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let handler = ListModules::new(runtime);
        let request = Request::new(
            Method::Get,
            Uri::from_str("http://localhost/modules").unwrap(),
        );

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        response
            .body()
            .concat2()
            .and_then(|b| {
                let list: ModuleList = serde_json::from_slice(&b).unwrap();
                let module = list.modules().iter().next().unwrap();

                assert_eq!("test-module", module.name());
                assert_eq!("test", module.type_());

                let config: TestConfig = serde_json::from_value(
                    serde_json::to_value(module.config().settings()).unwrap(),
                ).unwrap();
                assert_eq!("microsoft/test-image", config.image());

                assert_eq!("0", module.status().exit_status().unwrap().status_code());
                assert_eq!(
                    "2018-04-13T15:20:00.001+00:00",
                    module.status().exit_status().unwrap().exit_time()
                );
                assert_eq!(
                    "2018-04-13T14:20:00.001+00:00",
                    module.status().start_time().unwrap()
                );
                assert_eq!("running", module.status().runtime_status().status());
                assert_eq!(
                    "description",
                    module.status().runtime_status().description().unwrap()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn list_failed() {
        // arrange
        let runtime = TestRuntime::new(Err(Error::General));
        let handler = ListModules::new(runtime);
        let request = Request::new(
            Method::Get,
            Uri::from_str("http://localhost/modules").unwrap(),
        );

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Module runtime error\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn state_failed() {
        // arrange
        let config = TestConfig::new("microsoft/test-image".to_string());
        let module = TestModule::new("test-module".to_string(), config, Err(Error::General));
        let runtime = TestRuntime::new(Ok(module));
        let handler = ListModules::new(runtime);
        let request = Request::new(
            Method::Get,
            Uri::from_str("http://localhost/modules").unwrap(),
        );

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Module runtime error\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
