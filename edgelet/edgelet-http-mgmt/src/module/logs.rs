// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    module: String,

    follow: Option<String>,
    tail: Option<String>,
    since: Option<String>,
    until: Option<String>,
    timestamps: Option<String>,
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
        query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        _extensions: &http::Extensions,
    ) -> Option<Self> {
        let uri_regex = regex::Regex::new("^/modules/(?P<module>[^/]+)/logs$")
            .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module = &captures["module"];
        let module = percent_encoding::percent_decode_str(module)
            .decode_utf8()
            .ok()?;

        let follow = edgelet_http::find_query("follow", query);
        let tail = edgelet_http::find_query("tail", query);
        let since = edgelet_http::find_query("since", query);
        let until = edgelet_http::find_query("until", query);
        let timestamps = edgelet_http::find_query("timestamps", query);

        Some(Route {
            runtime: service.runtime.clone(),
            module: module.into_owned(),

            follow,
            tail,
            since,
            until,
            timestamps,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    async fn get(self) -> http_common::server::RouteResponse {
        let log_options = self.log_options()?;

        let logs = {
            let runtime = self.runtime.lock().await;

            runtime
                .logs(&self.module, &log_options)
                .await
                .map_err(|err| edgelet_http::error::server_error(err.to_string()))?
        };

        let res = http_common::server::response::chunked(hyper::StatusCode::OK, logs, "text/plain");
        Ok(res)
    }

    type PostBody = serde::de::IgnoredAny;

    type PutBody = serde::de::IgnoredAny;
}

impl<M> Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    fn log_options(&self) -> Result<edgelet_core::LogOptions, http_common::server::Error> {
        let mut log_options = edgelet_core::LogOptions::new();

        if let Some(follow) = &self.follow {
            let follow = std::str::FromStr::from_str(follow)
                .map_err(|_| edgelet_http::error::bad_request("invalid parameter: follow"))?;

            log_options = log_options.with_follow(follow);
        }

        if let Some(tail) = &self.tail {
            let tail = std::str::FromStr::from_str(tail)
                .map_err(|_| edgelet_http::error::bad_request("invalid parameter: tail"))?;

            log_options = log_options.with_tail(tail);
        }

        if let Some(since) = &self.since {
            let since = edgelet_core::parse_since(since)
                .map_err(|_| edgelet_http::error::bad_request("invalid parameter: since"))?;

            log_options = log_options.with_since(since);
        }

        if let Some(until) = &self.until {
            let until = edgelet_core::parse_since(until)
                .map_err(|_| edgelet_http::error::bad_request("invalid parameter: until"))?;

            log_options = log_options.with_until(until);
        }

        if let Some(timestamps) = &self.timestamps {
            let timestamps = std::str::FromStr::from_str(timestamps)
                .map_err(|_| edgelet_http::error::bad_request("invalid parameter: timestamps"))?;

            log_options = log_options.with_timestamps(timestamps);
        }

        Ok(log_options)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_test_utils::{test_route_err, test_route_ok};

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!("/modules/testModule/logs");
        assert_eq!("testModule", &route.module);

        // Missing module name
        test_route_err!("/modules//logs");

        // Extra character at beginning of URI
        test_route_err!("a/modules/testModule/logs");

        // Extra character at end of URI
        test_route_err!("/modules/testModule/logsa");
    }

    #[test]
    #[allow(clippy::bool_assert_comparison)]
    fn parse_query_bools() {
        let uri = "/modules/testModule/logs";

        // Boolean query keys to check, paired to a function pointer to get the parsed value.
        let keys = vec![
            (
                "follow",
                edgelet_core::LogOptions::follow
                    as for<'r> fn(&'r edgelet_core::LogOptions) -> bool,
            ),
            ("timestamps", edgelet_core::LogOptions::timestamps),
        ];

        for (key, get_key) in keys {
            // Default value when not provided
            let route = test_route_ok!(uri);
            let log_options = route.log_options().unwrap();
            assert_eq!(false, get_key(&log_options));

            // Valid value
            let route = test_route_ok!(uri, (key, "true"));
            let log_options = route.log_options().unwrap();
            assert_eq!(true, get_key(&log_options));

            let route = test_route_ok!(uri, (key, "false"));
            let log_options = route.log_options().unwrap();
            assert_eq!(false, get_key(&log_options));

            // Invalid value
            let route = test_route_ok!(uri, (key, "invalid"));
            assert!(route.log_options().is_err());
        }
    }

    #[test]
    fn parse_query_tail() {
        let uri = "/modules/testModule/logs";

        // Default value when not provided
        let route = test_route_ok!(uri);
        let log_options = route.log_options().unwrap();
        assert_eq!(&edgelet_core::LogTail::default(), log_options.tail());

        // Valid value
        let route = test_route_ok!(uri, ("tail", "all"));
        let log_options = route.log_options().unwrap();
        assert_eq!(&edgelet_core::LogTail::All, log_options.tail());

        let route = test_route_ok!(uri, ("tail", "5"));
        let log_options = route.log_options().unwrap();
        assert_eq!(&edgelet_core::LogTail::Num(5), log_options.tail());

        // Invalid value
        let route = test_route_ok!(uri, ("tail", "invalid"));
        assert!(route.log_options().is_err());
    }

    #[test]
    fn parse_query_since_until() {
        let uri = "/modules/testModule/logs";

        // Default value when not provided
        let route = test_route_ok!(uri);
        let log_options = route.log_options().unwrap();
        assert_eq!(0, log_options.since());

        let route = test_route_ok!(uri);
        let log_options = route.log_options().unwrap();
        assert_eq!(None, log_options.until());

        // Time query keys to check, paired to a function pointer to get the parsed value.
        // Wrap the until() function so its signature matches since()
        fn get_until(opts: &edgelet_core::LogOptions) -> i32 {
            opts.until().unwrap()
        }

        let keys = vec![
            (
                "since",
                edgelet_core::LogOptions::since as for<'r> fn(&'r edgelet_core::LogOptions) -> i32,
            ),
            ("until", get_until),
        ];

        for (key, get_key) in keys {
            // Valid value
            let route = test_route_ok!(uri, (key, "5"));
            let log_options = route.log_options().unwrap();
            assert_eq!(5, get_key(&log_options));

            let route = test_route_ok!(uri, (key, "-5"));
            let log_options = route.log_options().unwrap();
            assert_eq!(-5, get_key(&log_options));

            // Invalid value
            let route = test_route_ok!(uri, (key, "invalid"));
            assert!(route.log_options().is_err());
        }
    }

    #[test]
    fn parse_query_multi() {
        // Check that all log options can be parsed together.
        let route = test_route_ok!(
            "/modules/testModule/logs",
            ("follow", "true"),
            ("tail", "100"),
            ("since", "5"),
            ("until", "10"),
            ("timestamps", "true")
        );
        let log_options = route.log_options().unwrap();

        assert!(log_options.follow());
        assert_eq!(&edgelet_core::LogTail::Num(100), log_options.tail());
        assert_eq!(5, log_options.since());
        assert_eq!(Some(10), log_options.until());
        assert!(log_options.timestamps());
    }
}
