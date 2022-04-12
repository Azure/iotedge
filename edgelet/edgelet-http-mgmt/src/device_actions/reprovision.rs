// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    reprovision: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
    pid: libc::pid_t,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

const PATH: &str = "/device/reprovision";

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
        if path != PATH {
            return None;
        }

        let pid = match extensions.get::<Option<libc::pid_t>>().copied().flatten() {
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
    use http_common::server::Route;

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

    #[tokio::test]
    async fn auth() {
        async fn post(
            route: super::Route<edgelet_test_utils::runtime::Runtime>,
        ) -> http_common::server::RouteResponse {
            route.post(None).await
        }

        edgelet_test_utils::test_auth_agent!(super::PATH, post);
    }

    #[tokio::test]
    async fn reprovision_tx_rx() {
        let runtime = edgelet_test_utils::runtime::Runtime::default();
        let (service, mut reprovision_rx) = crate::Service::new_with_reprovision(runtime);

        let route = super::Route::from_uri(
            &service,
            super::PATH,
            &Vec::new(),
            &edgelet_test_utils::route::extensions(),
        )
        .expect("valid route wasn't parsed");

        let response = route.post(None).await.unwrap();
        assert_eq!(hyper::StatusCode::NO_CONTENT, response.status());

        // Calling reprovision should have sent a shutdown message now available on the receiver.
        let shutdown_reason = reprovision_rx.recv().await.unwrap();
        assert_eq!(edgelet_core::ShutdownReason::Reprovision, shutdown_reason);
    }
}
