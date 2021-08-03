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

            runtime.logs(&self.module, &log_options).await
        };

        let res = http_common::server::response::chunked(hyper::StatusCode::OK, logs);
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
            let since = edgelet_core::parse_since(&since)
                .map_err(|_| edgelet_http::error::bad_request("invalid parameter: since"))?;

            log_options = log_options.with_since(since);
        }

        if let Some(until) = &self.until {
            let until = edgelet_core::parse_since(&until)
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
