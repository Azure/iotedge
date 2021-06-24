// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route {
    pid: libc::pid_t,
}

#[async_trait::async_trait]
impl http_common::server::Route for Route {
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2019_10_22)..)
    }

    type Service = crate::DeviceManagement;
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

        Some(Route { pid })
    }

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();

    type GetResponse = ();

    type PostBody = serde::de::IgnoredAny;
    type PostResponse = ();
    async fn post(
        self,
        _body: Option<Self::PostBody>,
    ) -> http_common::server::RouteResponse<Option<Self::PostResponse>> {
        edgelet_http::auth_agent(self.pid)?;

        Ok((http::StatusCode::OK, None))
    }

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}
