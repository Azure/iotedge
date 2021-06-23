// Copyright (c) Microsoft. All rights reserved.

use crate::identity::Identity;

pub(crate) struct Route {
    client: std::sync::Arc<futures_util::lock::Mutex<aziot_identity_client_async::Client>>,
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
        _extensions: &http::Extensions,
    ) -> Option<Self> {
        if path != "/identities" {
            return None;
        }

        Some(Route {
            client: service.client.clone(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();

    type GetResponse = Vec<Identity>;
    async fn get(self) -> http_common::server::RouteResponse<Self::GetResponse> {
        todo!()
    }

    type PostBody = serde::de::IgnoredAny;
    type PostResponse = ();

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}
