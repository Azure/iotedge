// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::Future;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use serde::Serialize;
use serde_json;

use edgelet_core::{Module, ModuleRuntime, RuntimeOperation};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use management::models::*;

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct GetSystemInfo<M> {
    runtime: M,
}

impl<M> GetSystemInfo<M> {
    pub fn new(runtime: M) -> Self {
        GetSystemInfo { runtime }
    }
}

impl<M> Handler<Parameters> for GetSystemInfo<M>
where
    M: 'static + ModuleRuntime + Send,
    <M::Module as Module>::Config: Serialize,
{
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        debug!("Get System Information");

        let response = self
            .runtime
            .system_info()
            .then(|system_info| -> Result<_, Error> {
                let system_info = system_info
                    .context(ErrorKind::RuntimeOperation(RuntimeOperation::SystemInfo))?;

                let body = SystemInfo::new(
                    system_info.os_type().to_string(),
                    system_info.architecture().to_string(),
                    system_info.version().to_string(),
                );

                let b = serde_json::to_string(&body)
                    .context(ErrorKind::RuntimeOperation(RuntimeOperation::SystemInfo))?;

                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, b.len().to_string().as_str())
                    .body(b.into())
                    .context(ErrorKind::RuntimeOperation(RuntimeOperation::SystemInfo))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()));

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

    use super::*;
    use crate::server::module::tests::Error;

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
                assert_eq!(
                    "Could not query system info\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
