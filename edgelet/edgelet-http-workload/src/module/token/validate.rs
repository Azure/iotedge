use std::time::{SystemTime, UNIX_EPOCH};

use super::TokenValidateRequest;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    module_id: String,
    pid: libc::pid_t,
    api: super::TokenGeneratorAPI,
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
        let uri_regex = regex::Regex::new("^/modules/(?P<moduleId>[^/]+)/token/validate$")
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

        let api = super::TokenGeneratorAPI::new(
            service.key_client.clone(),
            service.identity_client.clone(),
            &service.config,
        );

        Some(Route {
            module_id: module_id.into_owned(),
            pid,
            api,
            runtime: service.runtime.clone(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    type PostBody = TokenValidateRequest;
    async fn post(self, body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        edgelet_http::auth_caller(&self.module_id, self.pid, &self.runtime).await?;

        let token = match body {
            Some(body) => body.token,
            None => return Err(edgelet_http::error::bad_request("missing request body")),
        };

        let expiry = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;
        let expiry = expiry.as_secs();

        self.api.validate_token(self.module_id, token, expiry).await
    }

    type PutBody = serde::de::IgnoredAny;
}
