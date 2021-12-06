// Copyright (c) Microsoft. All rights reserved.

use opentelemetry::{
    global,
    trace::{Span, Tracer, TracerProvider},
};

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    pid: libc::pid_t,
    module: String,
    start: Option<String>,
}

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync + 'static,
    <M as edgelet_core::ModuleRuntime>::Config: serde::de::DeserializeOwned + Sync,
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
        extensions: &http::Extensions,
    ) -> Option<Self> {
        let uri_regex = regex::Regex::new("^/modules/(?P<module>[^/]+)$")
            .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module = &captures["module"];
        let module = percent_encoding::percent_decode_str(module)
            .decode_utf8()
            .ok()?;
        let module = module.trim_start_matches('/');

        let start = edgelet_http::find_query("start", query);

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            runtime: service.runtime.clone(),
            pid,
            module: module.to_owned(),
            start,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;
    async fn delete(self, _body: Option<Self::DeleteBody>) -> http_common::server::RouteResponse {
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let mut span = tracer.start("module:delete");
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let runtime = self.runtime.lock().await;

        match runtime.remove(&self.module).await {
            Ok(_) => {
                span.end();
                Ok(http_common::server::response::no_content())
            }
            Err(err) => {
                span.end();
                Err(edgelet_http::error::server_error(err.to_string()))
            }
        }
    }

    async fn get(self) -> http_common::server::RouteResponse {
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let mut span = tracer.start("module:get");
        let runtime = self.runtime.lock().await;

        let module_info = runtime
            .get(&self.module)
            .await
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;

        let res: edgelet_http::ModuleDetails = module_info.into();
        let res = http_common::server::response::json(hyper::StatusCode::OK, &res);
        span.end();
        Ok(res)
    }

    type PostBody = serde::de::IgnoredAny;

    type PutBody = edgelet_http::ModuleSpec;
    async fn put(self, body: Self::PutBody) -> http_common::server::RouteResponse {
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let mut span = tracer.start("module:update");
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let start = if let Some(start) = &self.start {
            span.end();
            std::str::FromStr::from_str(start)
                .map_err(|_| edgelet_http::error::bad_request("invalid parameter: start"))?
        } else {
            span.end();
            false
        };

        // A special case is needed when restarting edgeAgent. Since edgeAgent is the module that
        // calls the management socket, we cannot restart it from this task because restarting
        // edgeAgent terminates its connection to the management socket and cancels this task.
        if self.module == "edgeAgent" {
            // Build a successful response.
            let details = if start {
                edgelet_http::ModuleDetails::from_spec(&body, edgelet_core::ModuleStatus::Running)
            } else {
                edgelet_http::ModuleDetails::from_spec(&body, edgelet_core::ModuleStatus::Stopped)
            };

            let res = http_common::server::response::json(hyper::StatusCode::CREATED, &details);

            // Assign the work to restart edgeAgent to a new task and return the successful response.
            // It doesn't matter if restarting edgeAgent fails because the aziot-edged watchdog will
            // retry on failure.
            tokio::spawn(async move { self.update_module(body, start).await });

            span.end();
            Ok(res)
        } else {
            span.end();
            self.update_module(body, start).await
        }
    }
}

impl<M> Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
    <M as edgelet_core::ModuleRuntime>::Config: serde::de::DeserializeOwned + Sync,
{
    async fn update_module(
        self,
        body: edgelet_http::ModuleSpec,
        start: bool,
    ) -> http_common::server::RouteResponse {
        let runtime = self.runtime.lock().await;

        // Stop module first so connections are closed gracefully...
        runtime
            .stop(&self.module, None)
            .await
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;

        // Then remove the module.
        runtime
            .remove(&self.module)
            .await
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;

        super::create_module(&*runtime, body.clone()).await?;

        let details = if start {
            match runtime.start(&self.module).await {
                Ok(()) => log::info!("Successfully started module {}", self.module),
                Err(err) => log::warn!("Failed to start module {}: {}", self.module, err),
            }

            edgelet_http::ModuleDetails::from_spec(&body, edgelet_core::ModuleStatus::Running)
        } else {
            edgelet_http::ModuleDetails::from_spec(&body, edgelet_core::ModuleStatus::Stopped)
        };

        let res = http_common::server::response::json(hyper::StatusCode::CREATED, &details);

        Ok(res)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_test_utils::{test_route_err, test_route_ok};

    const TEST_PATH: &str = "/modules/testModule";

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!(TEST_PATH);
        assert_eq!("testModule", &route.module);
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);
        assert!(route.start.is_none());

        // Valid URI with query parameter
        let route = test_route_ok!(TEST_PATH, ("start", "true"));
        assert_eq!("testModule", &route.module);
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);
        assert_eq!("true", route.start.unwrap());

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", TEST_PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}/", TEST_PATH));
    }
}
