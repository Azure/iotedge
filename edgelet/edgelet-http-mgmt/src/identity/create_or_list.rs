// Copyright (c) Microsoft. All rights reserved.

use std::convert::TryFrom;

#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;

#[cfg(test)]
use test_common::client::IdentityClient;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,
    pid: libc::pid_t,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct CreateIdentityRequest {
    #[serde(rename = "moduleId")]
    module_id: String,

    #[serde(rename = "managedBy", default = "super::default_managed_by")]
    managed_by: String,
}

#[derive(Debug, serde::Serialize)]
#[cfg_attr(test, derive(serde::Deserialize))]
pub(crate) struct ListIdentitiesResponse {
    identities: Vec<crate::identity::Identity>,
}

const PATH: &str = "/identities";

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
        // A bug in certain versions of edgeAgent causes it to make requests to "/identities/" instead
        // of "/identities". To maintain compatibility with these versions of edgeAgent, this API will
        // allow both endpoints.
        if path != PATH && path != "/identities/" {
            return None;
        }

        let pid = match extensions.get::<Option<libc::pid_t>>().copied().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            client: service.identity.clone(),
            pid,
            runtime: service.runtime.clone(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    async fn get(self) -> http_common::server::RouteResponse {
        let client = self.client.lock().await;

        let mut identities = vec![];
        match client.get_identities().await {
            Ok(ids) => {
                for identity in ids {
                    let identity = crate::identity::Identity::try_from(identity)?;
                    identities.push(identity);
                }
            }
            Err(err) => {
                return Err(edgelet_http::error::server_error(err.to_string()));
            }
        };

        let res = ListIdentitiesResponse { identities };
        let res = http_common::server::response::json(hyper::StatusCode::OK, &res);

        Ok(res)
    }

    type PostBody = CreateIdentityRequest;
    async fn post(self, body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let body = match body {
            Some(body) => body,
            None => {
                return Err(edgelet_http::error::bad_request("missing request body"));
            }
        };

        let client = self.client.lock().await;

        let identity = match client.create_module_identity(&body.module_id).await {
            Ok(identity) => {
                let mut identity = crate::identity::Identity::try_from(identity)?;
                identity.managed_by = body.managed_by;

                identity
            }
            Err(err) => {
                return Err(edgelet_http::error::server_error(err.to_string()));
            }
        };

        let res = http_common::server::response::json(hyper::StatusCode::OK, &identity);

        Ok(res)
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
            let body = super::CreateIdentityRequest {
                module_id: "testModule".to_string(),
                managed_by: crate::identity::default_managed_by(),
            };

            route.post(Some(body)).await
        }

        edgelet_test_utils::test_auth_agent!(super::PATH, post);
    }

    #[tokio::test]
    async fn create_get_identities() {
        let mut expected_identities = vec![];

        // The Identity Client needs to be persisted across API calls.
        let client = super::IdentityClient::default();
        let client = std::sync::Arc::new(futures_util::lock::Mutex::new(client));

        // Create identities
        for module in &["testModule1", "testModule2", "testModule3"] {
            let mut route = test_route_ok!(super::PATH);
            route.client = client.clone();

            let body = super::CreateIdentityRequest {
                module_id: module.to_string(),
                managed_by: crate::identity::default_managed_by(),
            };

            let response = route.post(Some(body)).await.unwrap();
            let body = hyper::body::to_bytes(response.into_body()).await.unwrap();
            let response: crate::identity::Identity = serde_json::from_slice(&body).unwrap();

            expected_identities.push(response);
        }

        // Get identities
        let mut route = test_route_ok!(super::PATH);
        route.client = client.clone();

        let response = route.get().await.unwrap();
        let body = hyper::body::to_bytes(response.into_body()).await.unwrap();
        let response: super::ListIdentitiesResponse = serde_json::from_slice(&body).unwrap();

        // Check that identities response contains the expected identities
        for identity in expected_identities {
            assert!(response.identities.contains(&identity));
        }
    }
}
