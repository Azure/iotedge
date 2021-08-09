// Copyright (c) Microsoft. All rights reserved.

use std::convert::TryFrom;

#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;

#[cfg(test)]
use edgelet_test_utils::client::IdentityClient;

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
pub(crate) struct ListIdentitiesResponse {
    identities: Vec<crate::identity::Identity>,
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
        if path != "/identities" {
            return None;
        }

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
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
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

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
