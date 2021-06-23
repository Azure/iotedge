// Copyright (c) Microsoft. All rights reserved.

use std::convert::TryFrom;

pub(crate) struct Route {
    client: std::sync::Arc<futures_util::lock::Mutex<aziot_identity_client_async::Client>>,
    pid: libc::pid_t,
}

#[derive(Debug, serde::Serialize)]
pub(crate) struct ListIdentitiesResponse {
    identities: Vec<crate::identity::Identity>,
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
        if path != "/identities" {
            return None;
        }

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            client: service.client.clone(),
            pid,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();

    type GetResponse = ListIdentitiesResponse;
    async fn get(self) -> http_common::server::RouteResponse<Self::GetResponse> {
        edgelet_http::auth_agent(self.pid)?;

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
                return Err(http_common::server::Error {
                    status_code: http::StatusCode::INTERNAL_SERVER_ERROR,
                    message: format!("{}", err).into(),
                })
            }
        };

        Ok((http::StatusCode::OK, ListIdentitiesResponse { identities }))
    }

    type PostBody = serde::de::IgnoredAny;
    type PostResponse = ();

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}
