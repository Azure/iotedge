// Copyright (c) Microsoft. All rights reserved.

use failure::{Fail, ResultExt};
use futures::{Future, IntoFuture};
use hyper::{Body, Request, Response, StatusCode};

use edgelet_core::{ModuleRuntime, RuntimeOperation};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;

use error::{Error, ErrorKind};
use IntoResponse;

pub struct StopModule<M> {
    runtime: M,
}

impl<M> StopModule<M> {
    pub fn new(runtime: M) -> Self {
        StopModule { runtime }
    }
}

impl<M> Handler<Parameters> for StopModule<M>
where
    M: 'static + ModuleRuntime + Send,
{
    fn handle(
        &self,
        _req: Request<Body>,
        params: Parameters,
    ) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .map(|name| {
                let name = name.to_string();

                self.runtime.stop(&name, None).then(|result| match result {
                    Ok(_) => Ok(name),
                    Err(err) => Err(Error::from(err.context(ErrorKind::RuntimeOperation(
                        RuntimeOperation::StopModule(name),
                    )))),
                })
            })
            .into_future()
            .flatten()
            .and_then(|name| {
                Ok(Response::builder()
                    .status(StatusCode::NO_CONTENT)
                    .body(Body::default())
                    .context(ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(
                        name,
                    )))?)
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
