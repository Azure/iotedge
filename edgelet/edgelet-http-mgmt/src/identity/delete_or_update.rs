// Copyright (c) Microsoft. All rights reserved.

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
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let client = self.client.lock().await;

        match client.delete_identity(&self.module_id).await {
            Ok(_) => Ok(http_common::server::response::no_content()),
            Err(err) => Err(edgelet_http::error::server_error(err.to_string())),
        }
    }

    type PostBody = serde::de::IgnoredAny;

    type PutBody = serde::de::IgnoredAny;
    async fn put(self, _body: Self::PutBody) -> http_common::server::RouteResponse {
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let client = self.client.lock().await;

        let identity = match client.update_module_identity(&self.module_id).await {
            Ok(identity) => crate::identity::Identity::try_from(identity)?,
            Err(err) => {
                return Err(edgelet_http::error::server_error(err.to_string()));
            }
        };

        let res = http_common::server::response::json(hyper::StatusCode::OK, &identity);

        Ok(res)
    }
}
