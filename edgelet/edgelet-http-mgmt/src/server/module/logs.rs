// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{LogOptions, LogTail, ModuleRuntime};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use failure::ResultExt;
use futures::{future, Future};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use url::form_urlencoded;

use error::{Error, ErrorKind};
use IntoResponse;

pub struct ModuleLogs<M>
where
    M: 'static + ModuleRuntime + Clone,
{
    runtime: M,
}

impl<M> ModuleLogs<M>
where
    M: 'static + ModuleRuntime + Clone,
{
    pub fn new(runtime: M) -> Self {
        ModuleLogs { runtime }
    }
}

impl<M> Handler<Parameters> for ModuleLogs<M>
where
    M: 'static + ModuleRuntime + Clone,
    M::Error: IntoResponse,
    M::Logs: Into<Body>,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        let runtime = self.runtime.clone();
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .and_then(|name| {
                let options = req.uri()
                    .query()
                    .map(parse_options)
                    .unwrap_or_else(|| Ok(LogOptions::default()))
                    .context(ErrorKind::BadParam);
                Ok((name, options?))
            })
            .map(|(name, options)| {
                let result = runtime
                    .logs(name, &options)
                    .map(|s| {
                        Response::builder()
                            .status(StatusCode::OK)
                            .body(s.into())
                            .unwrap_or_else(|e| e.into_response())
                    })
                    .or_else(|e| future::ok(e.into_response()));
                future::Either::A(result)
            })
            .unwrap_or_else(|e| future::Either::B(future::ok(e.into_response())));
        Box::new(response)
    }
}

fn parse_options(query: &str) -> Result<LogOptions, Error> {
    let parse = form_urlencoded::parse(query.as_bytes()).collect::<Vec<_>>();
    let tail = parse
        .iter()
        .find(|&(ref key, _)| key == "tail")
        .map(|(_, val)| val.parse::<LogTail>())
        .unwrap_or_else(|| Ok(LogTail::default()))?;
    let follow = parse
        .iter()
        .find(|&(ref key, _)| key == "follow")
        .map(|(_, val)| val.parse::<bool>())
        .unwrap_or_else(|| Ok(false))?;
    let options = LogOptions::new().with_follow(follow).with_tail(tail);
    Ok(options)
}

#[cfg(test)]
mod tests {
    use super::*;

    use chrono::prelude::*;
    use edgelet_core::{ModuleRuntimeState, ModuleStatus};
    use edgelet_test_utils::module::*;
    use futures::Stream;
    use management::models::*;
    use serde_json;
    use server::module::tests::Error;

    #[test]
    fn correct_logoptions() {
        let query = "follow=true&tail=6";
        let options = parse_options(&query).unwrap();
        assert_eq!(LogTail::Num(6), *options.tail());
        assert_eq!(true, options.follow());
    }

    #[test]
    fn logoption_defaults() {
        let query = "";
        let options = parse_options(&query).unwrap();
        assert_eq!(LogTail::default(), *options.tail());
        assert_eq!(false, options.follow());
    }

    #[test]
    fn logoption_follow_error() {
        let query = "follow=34&tail=6";
        let options = parse_options(&query);
        assert!(options.is_err());
        assert_eq!("Parse error", options.err().unwrap().to_string());
    }

    #[test]
    fn logoption_tail_error() {
        let query = "follow=false&tail=adsaf";
        let options = parse_options(&query);
        assert!(options.is_err());
        assert_eq!("Core error", options.err().unwrap().to_string());
    }

    #[test]
    fn test_success() {
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
        let handler = ModuleLogs::new(runtime);
        let request = Request::get("http://localhost/modules/mod1/logs?api-version=2018-06-28")
            .body(Body::default())
            .unwrap();
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "mod1".to_string())]);

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::OK, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                assert_eq!(0, b.len());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn runtime_error() {
        let runtime = TestRuntime::new(Err(Error::General));
        let handler = ModuleLogs::new(runtime);
        let request = Request::get("http://localhost/modules/mod1/logs?api-version=2018-06-28")
            .body(Body::default())
            .unwrap();
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "mod1".to_string())]);

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
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

    #[test]
    fn bad_params_fails() {
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
        let handler = ModuleLogs::new(runtime);
        let request = Request::get(
            "http://localhost/modules/mod1/logs?api-version=2018-06-28&follow=asfda&tail=asfafda",
        ).body(Body::default())
            .unwrap();
        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "mod1".to_string())]);

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!("Bad parameter\n\tcaused by: Core error\n\tcaused by: Parse error.\n\tcaused by: invalid digit found in string", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
