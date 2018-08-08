// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Module, ModuleRuntime};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use failure::ResultExt;
use futures::{future, Future};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use management::models::*;
use serde::Serialize;
use serde_json;

use error::ErrorKind;
use IntoResponse;

pub struct GetSystemInfo<M>
where
    M: 'static + ModuleRuntime,
    M::Error: IntoResponse,
    <M::Module as Module>::Config: Serialize,
{
    runtime: M,
}

impl<M> GetSystemInfo<M>
where
    M: 'static + ModuleRuntime,
    M::Error: IntoResponse,
    <M::Module as Module>::Config: Serialize,
{
    pub fn new(runtime: M) -> Self {
        GetSystemInfo { runtime }
    }
}

impl<M> Handler<Parameters> for GetSystemInfo<M>
where
    M: 'static + ModuleRuntime,
    M::Error: IntoResponse,
    <M::Module as Module>::Config: Serialize,
{
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        debug!("Get System Information");
        let response = self
            .runtime
            .system_info()
            .and_then(|systeminfo| {
                let body = SystemInfo::new(
                    systeminfo.os_type().to_string(),
                    systeminfo.architecture().to_string(),
                    systeminfo.version().to_string(),
                );
                let response = serde_json::to_string(&body)
                    .context(ErrorKind::Serde)
                    .map(|b| {
                        Response::builder()
                            .status(StatusCode::OK)
                            .header(CONTENT_TYPE, "application/json")
                            .header(CONTENT_LENGTH, b.len().to_string().as_str())
                            .body(b.into())
                            .unwrap_or_else(|e| e.into_response())
                    })
                    .unwrap_or_else(|e| e.into_response());
                future::ok(response)
            })
            .or_else(|e| future::ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_core::{self, ModuleRuntimeState};
    use edgelet_http::route::Parameters;
    use edgelet_test_utils::module::*;
    use futures::Stream;
    use management::models::SystemInfo;
    use server::module::tests::Error;

    use super::*;

    #[test]
    fn system_info_success() {
        // arrange
        let state = ModuleRuntimeState::default();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let handler = GetSystemInfo::new(runtime);
        let request = Request::get("http://localhost/info")
            .body(Body::default())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let system_info: SystemInfo = serde_json::from_slice(&b).unwrap();
                let os_type = system_info.os_type();
                let architecture = system_info.architecture();

                assert_eq!("os_type_sample", os_type);
                assert_eq!("architecture_sample", architecture);
                assert_eq!(edgelet_core::version(), system_info.version());

                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn system_info_failed() {
        // arrange
        let runtime = TestRuntime::new(Err(Error::General));
        let handler = GetSystemInfo::new(runtime);
        let request = Request::get("http://localhost/modules")
            .body(Body::default())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
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
}
