// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::{Future, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use serde::Serialize;
use serde_json;

use edgelet_core::{Module, ModuleRuntime, ModuleRuntimeState, RuntimeOperation};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use management::models::*;

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct ListModules<M> {
    runtime: M,
}

impl<M> ListModules<M> {
    pub fn new(runtime: M) -> Self {
        ListModules { runtime }
    }
}

impl<M> Handler<Parameters> for ListModules<M>
where
    M: 'static + ModuleRuntime + Send,
    <M::Module as Module>::Config: Serialize,
{
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        debug!("List modules");

        let response = self
            .runtime
            .list_with_details()
            .collect()
            .then(|result| -> Result<_, Error> {
                let details: Result<_, Error> = result
                    .context(ErrorKind::RuntimeOperation(RuntimeOperation::ListModules))?
                    .into_iter()
                    .map(|(module, state)| core_to_details(&module, &state))
                    .collect();
                let body = ModuleList::new(details?);
                let b = serde_json::to_string(&body)
                    .context(ErrorKind::RuntimeOperation(RuntimeOperation::ListModules))?;
                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, b.len().to_string().as_str())
                    .body(b.into())
                    .context(ErrorKind::RuntimeOperation(RuntimeOperation::ListModules))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

fn core_to_details<M>(module: &M, state: &ModuleRuntimeState) -> Result<ModuleDetails, Error>
where
    M: 'static + Module + Send,
    M::Config: Serialize,
{
    let settings = serde_json::to_value(module.config())
        .context(ErrorKind::RuntimeOperation(RuntimeOperation::ListModules))?;
    let config = Config::new(settings).with_env(vec![]);
    let mut runtime_status = RuntimeStatus::new(state.status().to_string());
    if let Some(description) = state.status_description() {
        runtime_status.set_description(description.to_string());
    }
    let mut status = Status::new(runtime_status);
    if let Some(started_at) = state.started_at() {
        status.set_start_time(started_at.to_rfc3339());
    }
    if let Some(code) = state.exit_code() {
        if let Some(finished_at) = state.finished_at() {
            status.set_exit_status(ExitStatus::new(finished_at.to_rfc3339(), code.to_string()));
        }
    }

    Ok(ModuleDetails::new(
        "id".to_string(),
        module.name().to_string(),
        module.type_().to_string(),
        config,
        status,
    ))
}

#[cfg(test)]
mod tests {
    use chrono::prelude::*;
    use edgelet_core::{ModuleRuntimeState, ModuleStatus};
    use edgelet_http::route::Parameters;
    use edgelet_test_utils::module::*;
    use futures::Stream;
    use management::models::ModuleList;

    use super::*;
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
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let handler = ListModules::new(runtime);
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
                let list: ModuleList = serde_json::from_slice(&b).unwrap();
                let module = list.modules().iter().next().unwrap();

                assert_eq!("test-module", module.name());
                assert_eq!("test", module.type_());

                let config: TestConfig = serde_json::from_value(
                    serde_json::to_value(module.config().settings()).unwrap(),
                )
                .unwrap();
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
                    "Could not list modules\n\tcaused by: General error",
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
                    "Could not list modules\n\tcaused by: General error",
                    error.message()
                );
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
