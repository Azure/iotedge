// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::ModuleRuntime;
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use futures::{future, Future};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};

use error::{Error, ErrorKind};
use IntoResponse;

pub struct StopModule<M>
where
    M: 'static + ModuleRuntime,
    <M as ModuleRuntime>::Error: IntoResponse,
{
    runtime: M,
}

impl<M> StopModule<M>
where
    M: 'static + ModuleRuntime,
    <M as ModuleRuntime>::Error: IntoResponse,
{
    pub fn new(runtime: M) -> Self {
        StopModule { runtime }
    }
}

impl<M> Handler<Parameters> for StopModule<M>
where
    M: 'static + ModuleRuntime,
    <M as ModuleRuntime>::Error: IntoResponse,
{
    fn handle(
        &self,
        _req: Request<Body>,
        params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .map(|name| {
                let result = self
                    .runtime
                    .stop(name, None)
                    .map(|_| {
                        Response::builder()
                            .status(StatusCode::NO_CONTENT)
                            .body(Body::default())
                            .unwrap_or_else(|e| e.into_response())
                    })
                    .or_else(|e| future::ok(e.into_response()));
                future::Either::A(result)
            })
            .unwrap_or_else(|e| future::Either::B(future::ok(e.into_response())));
        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use chrono::prelude::*;
    use edgelet_core::{ModuleRuntimeState, ModuleStatus};
    use edgelet_http::route::Parameters;
    use edgelet_test_utils::module::*;
    use server::module::tests::Error;

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
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let handler = StopModule::new(runtime);
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "test".to_string())]);
        let request = Request::post("http://localhost/modules/test/stop")
            .body(Body::default())
            .unwrap();

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::NO_CONTENT, response.status());
    }

    #[test]
    fn stop_bad_params() {
        // arrange
        let state = ModuleRuntimeState::default()
            .with_status(ModuleStatus::Running)
            .with_exit_code(Some(0))
            .with_status_description(Some("description".to_string()))
            .with_started_at(Some(Utc.ymd(2018, 4, 13).and_hms_milli(14, 20, 0, 1)))
            .with_finished_at(Some(Utc.ymd(2018, 4, 13).and_hms_milli(15, 20, 0, 1)))
            .with_image_id(Some("image-id".to_string()));
        let config = TestConfig::new("microsoft/test-image".to_string());
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let handler = StopModule::new(runtime);
        let request = Request::post("http://localhost/modules/test/stop")
            .body(Body::default())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
    }
}
