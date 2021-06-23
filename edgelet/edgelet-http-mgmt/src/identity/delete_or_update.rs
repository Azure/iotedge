// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route {
    client: std::sync::Arc<futures_util::lock::Mutex<aziot_identity_client_async::Client>>,
    pid: libc::pid_t,
    module_id: String,
}

#[async_trait::async_trait]
impl http_common::server::Route for Route {
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2018_06_28)..)
    }

    type Service = crate::IdentityManagement;
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
            client: service.client.clone(),
            pid,
            module_id: module_id.into_owned(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();
    async fn delete(
        self,
        _body: Option<Self::DeleteBody>,
    ) -> http_common::server::RouteResponse<Option<Self::DeleteResponse>> {
        edgelet_http::auth_agent(self.pid)?;

        let client = self.client.lock().await;

        match client.delete_identity(&self.module_id).await {
            Ok(_) => Ok((http::StatusCode::NO_CONTENT, None)),
            Err(err) => Err(http_common::server::Error {
                status_code: http::StatusCode::INTERNAL_SERVER_ERROR,
                message: format!("{}", err).into(),
            }),
        }
    }

    type GetResponse = ();

    type PostBody = serde::de::IgnoredAny;
    type PostResponse = ();

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}
