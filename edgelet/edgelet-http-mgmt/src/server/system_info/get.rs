// Copyright (c) Microsoft. All rights reserved.

use anyhow::Context;
use futures::Future;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use serde::Serialize;

use edgelet_core::{Module, ModuleRuntime, RuntimeOperation};
use edgelet_http::route::{Handler, Parameters};

use crate::error::Error;

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
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        debug!("Get System Information");

        let response = self
            .runtime
            .system_info()
            .then(|system_info| -> anyhow::Result<_> {
                let system_info = system_info
                    .context(Error::RuntimeOperation(RuntimeOperation::SystemInfo))?;

                let body = serde_json::to_string(&system_info)
                    .context(Error::RuntimeOperation(RuntimeOperation::SystemInfo))?;

                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, body.len().to_string().as_str())
                    .body(body.into())
                    .context(Error::RuntimeOperation(RuntimeOperation::SystemInfo))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.downcast::<Error>().map_or_else(edgelet_http::error::catchall_error_response, Into::into)));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_core::{self, MakeModuleRuntime, ModuleAction, ModuleRuntimeState};
    use edgelet_http::route::Parameters;
    use edgelet_test_utils::module::{TestConfig, TestModule, TestRuntime, TestSettings};
    use futures::{sync::mpsc, Stream};
    use management::models::SystemInfo;

    use super::{Body, Future, GetSystemInfo, Handler, Request};
    use crate::server::module::tests::Error;

    #[test]
    fn system_info_success() {
        // arrange
        let state = ModuleRuntimeState::default();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let module: TestModule<Error, _> =
            TestModule::new("test-module".to_string(), config, Ok(state));

        let (create_socket_channel_snd, _create_socket_channel_rcv) =
            mpsc::unbounded::<ModuleAction>();

        let runtime = TestRuntime::make_runtime(TestSettings::new(), create_socket_channel_snd)
            .wait()
            .unwrap()
            .with_module(Ok(module));
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

                assert_eq!("linux", os_type);
                assert_eq!("architecture_sample", architecture);
                assert_eq!(
                    edgelet_core::version_with_source_version(),
                    system_info.version(),
                );

                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn system_info_failed() {
        let (create_socket_channel_snd, _create_socket_channel_rcv) =
            mpsc::unbounded::<ModuleAction>();

        // arrange
        let runtime = TestRuntime::make_runtime(TestSettings::new(), create_socket_channel_snd)
            .wait()
            .unwrap()
            .with_module(Err(Error::General));
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
                let error: management::models::ErrorResponse = serde_json::from_slice(&b).unwrap();
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
