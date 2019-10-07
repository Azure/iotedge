// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::{future, Future, IntoFuture};
use hyper::{Body, Request, Response, StatusCode};
use url::form_urlencoded;

use edgelet_core::{LogOptions, LogTail, ModuleRuntime, RuntimeOperation};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct ModuleLogs<M> {
    runtime: M,
}

impl<M> ModuleLogs<M> {
    pub fn new(runtime: M) -> Self {
        ModuleLogs { runtime }
    }
}

impl<M> Handler<Parameters> for ModuleLogs<M>
where
    M: 'static + ModuleRuntime + Clone + Send,
    M::Logs: Into<Body>,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let runtime = self.runtime.clone();

        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .and_then(|name| {
                let name = name.to_string();
                let options = req
                    .uri()
                    .query()
                    .map_or_else(|| Ok(LogOptions::default()), parse_options)?;
                Ok((name, options))
            })
            .map(move |(name, options)| {
                runtime.logs(&name, &options).then(|s| -> Result<_, Error> {
                    let s = s.with_context(|_| {
                        ErrorKind::RuntimeOperation(RuntimeOperation::GetModuleLogs(name.clone()))
                    })?;
                    let response = Response::builder()
                        .status(StatusCode::OK)
                        .body(s.into())
                        .context(ErrorKind::RuntimeOperation(
                            RuntimeOperation::GetModuleLogs(name),
                        ))?;
                    Ok(response)
                })
            })
            .into_future()
            .flatten()
            .or_else(|e| future::ok(e.into_response()));

        Box::new(response)
    }
}

fn parse_options(query: &str) -> Result<LogOptions, Error> {
    let parse: Vec<_> = form_urlencoded::parse(query.as_bytes()).collect();
    let tail = parse
        .iter()
        .find(|&(ref key, _)| key == "tail")
        .map_or_else(|| Ok(LogTail::default()), |(_, val)| val.parse::<LogTail>())
        .context(ErrorKind::MalformedRequestParameter("tail"))?;
    let follow = parse
        .iter()
        .find(|&(ref key, _)| key == "follow")
        .map_or_else(|| Ok(false), |(_, val)| val.parse::<bool>())
        .context(ErrorKind::MalformedRequestParameter("follow"))?;
    let since = parse
        .iter()
        .find(|&(ref key, _)| key == "since")
        .map_or_else(|| Ok(0), |(_, val)| val.parse::<i32>())
        .context(ErrorKind::MalformedRequestParameter("since"))?;
    let options = LogOptions::new()
        .with_follow(follow)
        .with_tail(tail)
        .with_since(since);
    Ok(options)
}

#[cfg(test)]
mod tests {
    use chrono::prelude::*;
    use edgelet_core::{MakeModuleRuntime, ModuleRuntimeState, ModuleStatus};
    use edgelet_test_utils::crypto::TestHsm;
    use edgelet_test_utils::module::*;
    use futures::Stream;
    use management::models::*;
    use serde_json;

    use super::*;
    use crate::server::module::tests::Error;

    #[test]
    fn correct_logoptions() {
        let query = "follow=true&tail=6&since=1551885923";
        let options = parse_options(&query).unwrap();
        assert_eq!(LogTail::Num(6), *options.tail());
        assert_eq!(true, options.follow());
        assert_eq!(1_551_885_923, options.since());
    }

    #[test]
    fn logoption_defaults() {
        let query = "";
        let options = parse_options(&query).unwrap();
        assert_eq!(LogTail::default(), *options.tail());
        assert_eq!(false, options.follow());
        assert_eq!(0, options.since());
    }

    #[test]
    fn logoption_follow_error() {
        let query = "follow=34&tail=6";
        let options = parse_options(&query);
        assert!(options.is_err());
        assert_eq!(
            "The request parameter `follow` is malformed",
            options.err().unwrap().to_string()
        );
    }

    #[test]
    fn logoption_tail_error() {
        let query = "follow=false&tail=adsaf";
        let options = parse_options(&query);
        assert!(options.is_err());
        assert_eq!(
            "The request parameter `tail` is malformed",
            options.err().unwrap().to_string()
        );
    }

    #[test]
    fn logoption_since_error() {
        let query = "follow=true&tail=6&since=15abc";
        let options = parse_options(&query);
        assert!(options.is_err());
        assert_eq!(
            "The request parameter `since` is malformed",
            options.err().unwrap().to_string()
        );
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
        let module: TestModule<Error, _> = TestModule::new_with_logs(
            "test-module".to_string(),
            config,
            Ok(state),
            vec![&[b'A', b'B', b'C']],
        );
        let runtime = TestRuntime::make_runtime(
            TestSettings::new(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
        .wait()
        .unwrap()
        .with_module(Ok(module));
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
                assert_eq!(3, b.len());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn runtime_error() {
        let runtime = TestRuntime::make_runtime(
            TestSettings::new(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
        .wait()
        .unwrap()
        .with_module(Err(Error::General));
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
                assert_eq!(
                    "Could not get logs for module mod1\n\tcaused by: General error",
                    error.message()
                );
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
        let module: TestModule<Error, _> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::make_runtime(
            TestSettings::new(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
        .wait()
        .unwrap()
        .with_module(Ok(module));
        let handler = ModuleLogs::new(runtime);
        let request = Request::get(
            "http://localhost/modules/mod1/logs?api-version=2018-06-28&follow=asfda&tail=asfafda",
        )
        .body(Body::default())
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
                assert_eq!("The request parameter `tail` is malformed\n\tcaused by: Invalid log tail \"asfafda\"\n\tcaused by: invalid digit found in string", error.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
