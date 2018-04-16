// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;

use edgelet_core::ModuleRuntime;
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use futures::{future, Future};
use hyper::{Error as HyperError, StatusCode};
use hyper::server::{Request, Response};

use error::{Error, ErrorKind};
use IntoResponse;

pub struct RestartModule<M>
where
    M: 'static + ModuleRuntime,
    <M as ModuleRuntime>::Error: IntoResponse,
{
    runtime: RefCell<M>,
}

impl<M> RestartModule<M>
where
    M: 'static + ModuleRuntime,
    <M as ModuleRuntime>::Error: IntoResponse,
{
    pub fn new(runtime: M) -> Self {
        RestartModule {
            runtime: RefCell::new(runtime),
        }
    }
}

impl<M> Handler<Parameters> for RestartModule<M>
where
    M: 'static + ModuleRuntime,
    <M as ModuleRuntime>::Error: IntoResponse,
{
    fn handle(&self, _req: Request, params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .map(|name| {
                let result = self.runtime
                    .borrow_mut()
                    .restart(name)
                    .map(|_| Response::new().with_status(StatusCode::NoContent))
                    .or_else(|e| future::ok(e.into_response()));
                future::Either::A(result)
            })
            .unwrap_or_else(|e| future::Either::B(future::ok(e.into_response())));
        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use chrono::prelude::*;
    use edgelet_core::{ModuleRuntimeState, ModuleStatus};
    use edgelet_http::route::Parameters;
    use hyper::{Method, StatusCode, Uri};
    use hyper::server::Request;
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
        let handler = RestartModule::new(runtime);
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "test".to_string())]);
        let request = Request::new(
            Method::Get,
            Uri::from_str("http://localhost/modules/test/restart").unwrap(),
        );

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::NoContent, response.status());
    }

    #[test]
    fn restart_bad_params() {
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
        let handler = RestartModule::new(runtime);
        let request = Request::new(
            Method::Get,
            Uri::from_str("http://localhost/modules/test/restart").unwrap(),
        );

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BadRequest, response.status());
    }
}
