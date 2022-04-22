// Copyright (c) Microsoft. All rights reserved.

use anyhow::Context;
use futures::{Future, IntoFuture};
use hyper::{Body, Request, Response, StatusCode};

use edgelet_core::{ModuleRuntime, RuntimeOperation};
use edgelet_http::route::{Handler, Parameters};

use crate::error::Error;

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
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        let response = params
            .name("name")
            .context(Error::MissingRequiredParameter("name"))
            .map(|name| {
                let name = name.to_string();

                self.runtime.stop(&name, None).then(|result|
                    result.with_context(|| Error::RuntimeOperation(RuntimeOperation::StopModule(name.clone())))
                    .map(|_| name)
                )
            })
            .into_future()
            .flatten()
            .and_then(|name| {
                Ok(Response::builder()
                    .status(StatusCode::NO_CONTENT)
                    .body(Body::default())
                    .context(Error::RuntimeOperation(RuntimeOperation::StopModule(
                        name,
                    )))?)
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
    use edgelet_test_utils::module::{TestConfig, TestModule, TestRuntime, TestSettings};
    use futures::sync::mpsc;

    use super::{Body, Future, Handler, Request, StatusCode, StopModule};
    use crate::server::module::tests::Error;

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
        let module: TestModule<Error, _> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let (create_socket_channel_snd, _create_socket_channel_rcv) =
            mpsc::unbounded::<ModuleAction>();

        let runtime = TestRuntime::make_runtime(TestSettings::new(), create_socket_channel_snd)
            .wait()
            .unwrap()
            .with_module(Ok(module));
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
        let module: TestModule<Error, _> =
            TestModule::new("test-module".to_string(), config, Ok(state));

        let (create_socket_channel_snd, _create_socket_channel_rcv) =
            mpsc::unbounded::<ModuleAction>();

        let runtime = TestRuntime::make_runtime(TestSettings::new(), create_socket_channel_snd)
            .wait()
            .unwrap()
            .with_module(Ok(module));
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
