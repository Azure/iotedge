// Copyright (c) Microsoft. All rights reserved.

use opentelemetry::{
    global,
    trace::{FutureExt, Span, TraceContextExt, Tracer, TracerProvider},
    Context,
};

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    pid: libc::pid_t,
}

const PATH: &str = "/modules";

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
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
        _query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        extensions: &http::Extensions,
    ) -> Option<Self> {
        // A bug in certain versions of the diagnostics image causes it to make requests to "/modules/"
        // instead of "/modules". To maintain compatibility with these versions of diagnostics, this API
        // will allow both endpoints.
        if path != PATH && path != "/modules/" {
            return None;
        }

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            runtime: service.runtime.clone(),
            pid,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    async fn get(self) -> http_common::server::RouteResponse {
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let span = tracer.start("edgelet-http-mgmt:module:list");
        let cx = Context::current_with_span(span);
        let runtime = self.runtime.lock().await;

        let modules = FutureExt::with_context(runtime.list_with_details(), cx.clone())
            .await
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;

        let res: edgelet_http::ListModulesResponse = modules.into();
        let res = http_common::server::response::json(hyper::StatusCode::OK, &res);

        Ok(res)
    }

    type PostBody = edgelet_http::ModuleSpec;
    async fn post(self, body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let mut span = tracer.start("edgelet-http-mgmt:module:create");
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let body = match body {
            Some(body) => body,
            None => {
                span.end();
                return Err(edgelet_http::error::bad_request("missing request body"));
            }
        };

        let details =
            edgelet_http::ModuleDetails::from_spec(&body, edgelet_core::ModuleStatus::Stopped);

        let runtime = self.runtime.lock().await;

        super::create_module(&*runtime, body).await?;
        let res = http_common::server::response::json(hyper::StatusCode::CREATED, &details);
        span.end();
        Ok(res)
    }

    type PutBody = serde::de::IgnoredAny;
}

#[cfg(test)]
mod tests {
    use edgelet_test_utils::{test_route_err, test_route_ok};

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!(super::PATH);
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", super::PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}a", super::PATH));
    }
}
