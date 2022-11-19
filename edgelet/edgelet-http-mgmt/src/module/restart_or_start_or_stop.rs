// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    runtime: std::sync::Arc<tokio::sync::Mutex<M>>,
    module: String,
    action: Action,
}

#[cfg_attr(test, derive(Debug, PartialEq))]
enum Action {
    Restart,
    Start,
    Stop,
}

impl std::str::FromStr for Action {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        let s = s.to_lowercase();

        match &s[..] {
            "restart" => Ok(Action::Restart),
            "start" => Ok(Action::Start),
            "stop" => Ok(Action::Stop),
            _ => Err(()),
        }
    }
}

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2018_06_28)..)
    }

    type Service = crate::Service<M>;
    fn from_uri(
        service: &Self::Service,
        path: &str,
        _query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        _extensions: &http::Extensions,
    ) -> Option<Self> {
        let uri_regex = regex::Regex::new("^/modules/(?P<module>[^/]+)/(?P<action>[^/]+)$")
            .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module = &captures["module"];
        let module = percent_encoding::percent_decode_str(module)
            .decode_utf8()
            .ok()?;
        let module = module.trim_start_matches('/');

        let action = &captures["action"];
        let action = percent_encoding::percent_decode_str(action)
            .decode_utf8()
            .ok()?;
        let action = std::str::FromStr::from_str(&action).ok()?;

        Some(Route {
            runtime: service.runtime.clone(),
            module: module.to_owned(),
            action,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    type PostBody = serde::de::IgnoredAny;
    async fn post(self, _body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        let runtime = self.runtime.lock().await;

        match self.action {
            Action::Restart => runtime.restart(&self.module).await,
            Action::Start => runtime.start(&self.module).await,
            Action::Stop => runtime.stop(&self.module, None).await,
        }
        .map_err(|err| edgelet_http::error::runtime_error(&*runtime, &err))?;

        Ok(http_common::server::response::no_content())
    }

    type PutBody = serde::de::IgnoredAny;
}

#[cfg(test)]
mod tests {
    use edgelet_test_utils::{test_route_err, test_route_ok};

    #[test]
    fn parse_uri() {
        // Valid URI: restart
        let route = test_route_ok!("/modules/testModule/restart");
        assert_eq!("testModule", &route.module);
        assert_eq!(super::Action::Restart, route.action);

        // Valid URI: start
        let route = test_route_ok!("/modules/testModule/start");
        assert_eq!("testModule", &route.module);
        assert_eq!(super::Action::Start, route.action);

        // Valid URI: stop
        let route = test_route_ok!("/modules/testModule/stop");
        assert_eq!("testModule", &route.module);
        assert_eq!(super::Action::Stop, route.action);

        // Invalid action
        test_route_err!("/modules/testModule/invalid");

        // Missing module name
        test_route_err!("/modules//restart");
    }
}
