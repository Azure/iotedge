// Copyright (c) Microsoft. All rights reserved.

use opentelemetry::{global, trace::{Span, Tracer, TracerProvider}};
use std::convert::TryFrom;

#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;

#[cfg(test)]
use edgelet_test_utils::clients::IdentityClient;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,
    pid: libc::pid_t,
    module_id: String,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
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
        extensions: &http::Extensions,
    ) -> Option<Self> {
        let uri_regex = regex::Regex::new("^/identities/(?P<moduleId>[^/]+)$")
            .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module_id = &captures["moduleId"];
        let module_id = percent_encoding::percent_decode_str(module_id)
            .decode_utf8()
            .ok()?;

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            client: service.identity.clone(),
            pid,
            module_id: module_id.into_owned(),
            runtime: service.runtime.clone(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;
    async fn delete(self, _body: Option<Self::DeleteBody>) -> http_common::server::RouteResponse {
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let mut span = tracer.start("identity:delete");
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let client = self.client.lock().await;

        match client.delete_identity(&self.module_id).await {
            Ok(_) => {
                span.end();
                Ok(http_common::server::response::no_content())
            },
            Err(err) => {
                span.end();
                Err(edgelet_http::error::server_error(err.to_string()))
            },
        }
    }

    type PostBody = serde::de::IgnoredAny;

    type PutBody = serde::de::IgnoredAny;
    async fn put(self, _body: Self::PutBody) -> http_common::server::RouteResponse {
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let mut span = tracer.start("identity:put");
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let client = self.client.lock().await;

        let identity = match client.update_module_identity(&self.module_id).await {
            Ok(identity) => crate::identity::Identity::try_from(identity)?,
            Err(err) => {
                span.end();
                return Err(edgelet_http::error::server_error(err.to_string()));
            }
        };

        let res = http_common::server::response::json(hyper::StatusCode::OK, &identity);
        span.end();
        Ok(res)
    }
}

#[cfg(test)]
mod tests {
    use http_common::server::Route;

    use edgelet_test_utils::{test_route_err, test_route_ok};

    const TEST_PATH: &str = "/identities/testModule";

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!(TEST_PATH);
        assert_eq!("testModule", &route.module_id);
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);

        // Missing module ID
        test_route_err!("/identities/");

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", TEST_PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}/", TEST_PATH));
    }

    #[tokio::test]
    async fn auth() {
        async fn delete(
            route: super::Route<edgelet_test_utils::runtime::Runtime>,
        ) -> http_common::server::RouteResponse {
            route.delete(None).await
        }

        async fn put(
            route: super::Route<edgelet_test_utils::runtime::Runtime>,
        ) -> http_common::server::RouteResponse {
            route.put(serde::de::IgnoredAny).await
        }

        edgelet_test_utils::test_auth_agent!(TEST_PATH, delete);
        edgelet_test_utils::test_auth_agent!(TEST_PATH, put);
    }

    #[tokio::test]
    async fn update_delete() {
        // The Identity Client needs to be persisted across API calls.
        let client = edgelet_test_utils::clients::IdentityClient::default();
        let client = std::sync::Arc::new(futures_util::lock::Mutex::new(client));

        // Update Identity
        let mut route = test_route_ok!(TEST_PATH);
        route.client = client.clone();

        let response = route.put(serde::de::IgnoredAny).await.unwrap();
        let body = hyper::body::to_bytes(response.into_body()).await.unwrap();
        let _response: crate::identity::Identity = serde_json::from_slice(&body).unwrap();

        // Delete Identity
        let mut route = test_route_ok!(TEST_PATH);
        route.client = client.clone();

        route.delete(None).await.unwrap();

        // Update Identity should now fail because the Identity was deleted
        let mut route = test_route_ok!(TEST_PATH);
        route.client = client.clone();

        route.put(serde::de::IgnoredAny).await.unwrap_err();
    }
}
