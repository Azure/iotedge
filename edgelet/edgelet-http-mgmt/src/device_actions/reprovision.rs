// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    reprovision: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
    pid: libc::pid_t,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2019_10_22)..)
    }

    type Service = crate::Service<M>;
    fn from_uri(
        service: &Self::Service,
        path: &str,
        _query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        extensions: &http::Extensions,
    ) -> Option<Self> {
        if path != "/device/reprovision" {
            return None;
        }

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            reprovision: service.reprovision.clone(),
            pid,
            runtime: service.runtime.clone(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    type PostBody = serde::de::IgnoredAny;
    async fn post(self, _body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        match self
            .reprovision
            .send(edgelet_core::ShutdownReason::Reprovision)
        {
            Ok(()) => Ok(http_common::server::response::no_content()),
            Err(_) => Err(edgelet_http::error::server_error(
                "failed to send reprovision request",
            )),
        }
    }

    type PutBody = serde::de::IgnoredAny;
}

#[cfg(test)]
mod tests {
    use edgelet_test_utils::{test_route_err, test_route_ok};

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!("/device/reprovision");
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);

        // Extra character at beginning of URI
        test_route_err!("a/device/reprovision");

        // Extra character at end of URI
        test_route_err!("/device/reprovisiona");
    }
}
